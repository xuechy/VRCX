use axum::{extract::State, Json};
use serde::Serialize;
use sqlx::FromRow;

use crate::state::AppState;

#[derive(Debug, Serialize, FromRow)]
pub struct UserRow {
	pub id: uuid::Uuid,
	pub name: String,
}

pub async fn list_users(State(state): State<AppState>) -> Json<Vec<UserRow>> {
	let rows = sqlx::query_as::<_, UserRow>("SELECT id, name FROM users ORDER BY name")
		.fetch_all(&state.db)
		.await
		.unwrap_or_default();
	Json(rows)
}