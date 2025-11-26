use std::{env, fs, net::SocketAddr, path::Path};

use axum::{
    extract::{Query, State},
    http::StatusCode,
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use sqlx::{sqlite::SqlitePoolOptions, Pool, Sqlite};
use time::OffsetDateTime;
use tower_http::{cors::CorsLayer, trace::TraceLayer};
use tracing::{error, info, Level};

#[derive(Clone)]
struct AppState {
    db: Pool<Sqlite>,
}

#[derive(Serialize)]
struct HealthResponse {
    ok: bool,
    version: String,
    time: String,
}

#[derive(Serialize, Deserialize)]
struct Note {
    user_id: String,
    edited_at: String,
    memo: String,
}

#[derive(Serialize)]
struct FavoriteItem {
    id: i64,
    created_at: String,
    item_id: String,
    group_name: Option<String>,
}

#[derive(Deserialize)]
struct NotesQuery {
    user_id: Option<String>,
}

#[derive(Deserialize)]
struct FeedQuery {
    limit: Option<i64>,
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    init_tracing();

    let database_url = env::var("DATABASE_URL").unwrap_or_else(|_| "sqlite:///app/data/vrcx.db".to_string());
    ensure_parent_dir_exists(&database_url)?;

    let pool = SqlitePoolOptions::new()
        .max_connections(10)
        .connect(&database_url)
        .await?;

    apply_migrations(&pool).await?;

    let state = AppState { db: pool };

    let app = Router::new()
        .route("/healthz", get(health))
        .route("/api/notes", get(list_notes).post(upsert_note))
        .route("/api/favorites/worlds", get(list_favorite_worlds))
        .route("/api/favorites/avatars", get(list_favorite_avatars))
        .route("/api/feed/recent", get(list_recent_feed))
        .with_state(state)
        .layer(CorsLayer::permissive())
        .layer(TraceLayer::new_for_http());

    let addr = SocketAddr::from(([0, 0, 0, 0], 8080));
    info!("listening on {}", addr);
    axum::Server::bind(&addr).serve(app.into_make_service()).await?;
    Ok(())
}

fn init_tracing() {
    let env_filter = std::env::var("RUST_LOG").unwrap_or_else(|_| "info,tower_http=info".to_string());
    tracing_subscriber::fmt()
        .with_env_filter(env_filter)
        .with_target(true)
        .with_max_level(Level::INFO)
        .init();
}

fn ensure_parent_dir_exists(database_url: &str) -> anyhow::Result<()> {
    // expects format sqlite:///absolute/path.db or sqlite://relative.db
    if let Some(path_part) = database_url.strip_prefix("sqlite://") {
        let path = Path::new(path_part);
        if let Some(parent) = path.parent() {
            if !parent.exists() {
                fs::create_dir_all(parent)?;
            }
        }
    }
    Ok(())
}

async fn apply_migrations(pool: &Pool<Sqlite>) -> anyhow::Result<()> {
    // Minimal set of tables based on the existing app's SQLite usage
    sqlx::query(
        r#"
        CREATE TABLE IF NOT EXISTS memos (
            user_id TEXT PRIMARY KEY,
            edited_at TEXT,
            memo TEXT
        );
        "#,
    )
    .execute(pool)
    .await?;

    sqlx::query(
        r#"
        CREATE TABLE IF NOT EXISTS favorite_world (
            id INTEGER PRIMARY KEY,
            created_at TEXT,
            world_id TEXT,
            group_name TEXT
        );
        "#,
    )
    .execute(pool)
    .await?;

    sqlx::query(
        r#"
        CREATE TABLE IF NOT EXISTS favorite_avatar (
            id INTEGER PRIMARY KEY,
            created_at TEXT,
            avatar_id TEXT,
            group_name TEXT
        );
        "#,
    )
    .execute(pool)
    .await?;

    sqlx::query(
        r#"
        CREATE TABLE IF NOT EXISTS gamelog_event (
            id INTEGER PRIMARY KEY,
            created_at TEXT,
            data TEXT,
            UNIQUE(created_at, data)
        );
        "#,
    )
    .execute(pool)
    .await?;

    Ok(())
}

async fn health() -> impl IntoResponse {
    let now = OffsetDateTime::now_utc();
    let body = Json(HealthResponse {
        ok: true,
        version: env!("CARGO_PKG_VERSION").to_string(),
        time: now.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
    });
    (StatusCode::OK, body)
}

async fn list_notes(State(state): State<AppState>, Query(q): Query<NotesQuery>) -> impl IntoResponse {
    let rows = if let Some(user_id) = q.user_id {
        sqlx::query_as::<_, (String, String, String)>(
            "SELECT user_id, edited_at, memo FROM memos WHERE user_id = ?",
        )
        .bind(user_id)
        .fetch_all(&state.db)
        .await
    } else {
        sqlx::query_as::<_, (String, String, String)>(
            "SELECT user_id, edited_at, memo FROM memos ORDER BY edited_at DESC LIMIT 200",
        )
        .fetch_all(&state.db)
        .await
    };

    match rows {
        Ok(items) => {
            let notes: Vec<Note> = items
                .into_iter()
                .map(|(user_id, edited_at, memo)| Note { user_id, edited_at, memo })
                .collect();
            (StatusCode::OK, Json(notes)).into_response()
        }
        Err(err) => {
            error!(?err, "failed to list notes");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to list notes").into_response()
        }
    }
}

#[derive(Deserialize)]
struct UpsertNotePayload {
    user_id: String,
    memo: String,
}

async fn upsert_note(State(state): State<AppState>, Json(payload): Json<UpsertNotePayload>) -> impl IntoResponse {
    let edited_at = OffsetDateTime::now_utc()
        .format(&time::format_description::well_known::Rfc3339)
        .unwrap_or_else(|_| "".to_string());

    let result = sqlx::query(
        "INSERT INTO memos (user_id, edited_at, memo) VALUES (?, ?, ?)\n         ON CONFLICT(user_id) DO UPDATE SET edited_at = excluded.edited_at, memo = excluded.memo",
    )
    .bind(&payload.user_id)
    .bind(&edited_at)
    .bind(&payload.memo)
    .execute(&state.db)
    .await;

    match result {
        Ok(_) => StatusCode::NO_CONTENT.into_response(),
        Err(err) => {
            error!(?err, "failed to upsert note");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to upsert note").into_response()
        }
    }
}

async fn list_favorite_worlds(State(state): State<AppState>) -> impl IntoResponse {
    let rows = sqlx::query_as::<_, (i64, String, String, Option<String>)>(
        "SELECT id, created_at, world_id, group_name FROM favorite_world ORDER BY id DESC LIMIT 200",
    )
    .fetch_all(&state.db)
    .await;

    match rows {
        Ok(items) => {
            let list: Vec<FavoriteItem> = items
                .into_iter()
                .map(|(id, created_at, item_id, group_name)| FavoriteItem { id, created_at, item_id, group_name })
                .collect();
            (StatusCode::OK, Json(list)).into_response()
        }
        Err(err) => {
            error!(?err, "failed to list favorite worlds");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to list favorite worlds").into_response()
        }
    }
}

async fn list_favorite_avatars(State(state): State<AppState>) -> impl IntoResponse {
    let rows = sqlx::query_as::<_, (i64, String, String, Option<String>)>(
        "SELECT id, created_at, avatar_id, group_name FROM favorite_avatar ORDER BY id DESC LIMIT 200",
    )
    .fetch_all(&state.db)
    .await;

    match rows {
        Ok(items) => {
            let list: Vec<FavoriteItem> = items
                .into_iter()
                .map(|(id, created_at, item_id, group_name)| FavoriteItem { id, created_at, item_id, group_name })
                .collect();
            (StatusCode::OK, Json(list)).into_response()
        }
        Err(err) => {
            error!(?err, "failed to list favorite avatars");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to list favorite avatars").into_response()
        }
    }
}

async fn list_recent_feed(State(state): State<AppState>, Query(q): Query<FeedQuery>) -> impl IntoResponse {
    let limit = q.limit.unwrap_or(50).clamp(1, 500);
    let stmt = format!("SELECT id, created_at, data FROM gamelog_event ORDER BY id DESC LIMIT {}", limit);
    let rows = sqlx::query_as::<_, (i64, String, String)>(&stmt).fetch_all(&state.db).await;
    match rows {
        Ok(items) => (StatusCode::OK, Json(items)).into_response(),
        Err(err) => {
            error!(?err, "failed to list recent feed");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to list recent feed").into_response()
        }
    }
}

