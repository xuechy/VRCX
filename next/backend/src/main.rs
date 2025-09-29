use axum::{routing::get, Router};
use std::net::SocketAddr;
use tower_http::cors::{Any, CorsLayer};
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

mod state;
mod routes;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
	// Load env
	dotenvy::dotenv().ok();

	// Logging
	tracing_subscriber::registry()
		.with(tracing_subscriber::EnvFilter::from_default_env())
		.with(tracing_subscriber::fmt::layer())
		.init();

	let app_state = state::AppState::initialize().await?;

	let cors = CorsLayer::new()
		.allow_origin(Any)
		.allow_methods(Any)
		.allow_headers(Any);

	let app = Router::new()
		.route("/health", get(routes::health))
		.nest("/v1", routes::v1_router())
		.layer(cors)
		.with_state(app_state);

	let port: u16 = std::env::var("PORT")
		.ok()
		.and_then(|p| p.parse().ok())
		.unwrap_or(8080);
	let addr = SocketAddr::from(([0, 0, 0, 0], port));
	tracing::info!(%addr, "listening");
	axum::serve(tokio::net::TcpListener::bind(addr).await?, app)
		.await?;
	Ok(())
}