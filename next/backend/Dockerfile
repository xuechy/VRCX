# syntax=docker/dockerfile:1.6

FROM rust:1.82-slim AS dev
RUN apt-get update \
    && apt-get install -y --no-install-recommends pkg-config libssl-dev libsqlite3-dev ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN cargo install cargo-watch
WORKDIR /app
COPY Cargo.toml Cargo.lock* ./
RUN mkdir -p src \
    && printf "fn main() { println!(\"bootstrap\"); }" > src/main.rs \
    && cargo build \
    && rm -rf src
COPY . .
EXPOSE 8080
CMD ["bash","-lc","cargo watch -x 'run --bin vrcx_next_backend'"]

FROM rust:1.82-slim AS builder
RUN apt-get update \
    && apt-get install -y --no-install-recommends pkg-config libssl-dev libsqlite3-dev ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY . .
RUN cargo build --release

FROM debian:bookworm-slim AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends libssl3 libsqlite3-0 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=builder /app/target/release/vrcx_next_backend /usr/local/bin/vrcx_next_backend
ENV RUST_LOG=info
ENV DATABASE_URL=sqlite:///app/data/vrcx.db
RUN mkdir -p /app/data
EXPOSE 8080
CMD ["/usr/local/bin/vrcx_next_backend"]
