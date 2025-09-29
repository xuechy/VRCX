use sqlx::postgres::PgPoolOptions;
use sqlx::PgPool;

#[derive(Clone)]
pub struct AppState {
	pub db: PgPool,
}

impl AppState {
	pub async fn initialize() -> anyhow::Result<Self> {
		let database_url = std::env::var("DATABASE_URL")
			.unwrap_or_else(|_| "postgres://app:app@localhost:5432/app".to_string());
		let db = PgPoolOptions::new()
			.max_connections(10)
			.connect(&database_url)
			.await?;
		// Run migrations embedded at compile time from ./migrations
		sqlx::migrate!("./migrations").run(&db).await?;
		Ok(Self { db })
	}
}