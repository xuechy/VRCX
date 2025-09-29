use axum::{routing::get, Json, Router};
use serde_json::json;

use crate::state::AppState;

pub async fn health() -> Json<serde_json::Value> {
	Json(json!({ "ok": true }))
}

mod users;

pub fn v1_router() -> Router<AppState> {
	Router::new()
		.route("/ping", get(ping))
		.route("/users", get(users::list_users))
}

async fn ping() -> Json<serde_json::Value> {
	Json(json!({ "pong": true }))
}