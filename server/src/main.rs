mod server;
mod client_handler;
mod room_manager;
mod cards;


use server::Server;
use std::env;



#[tokio::main]
async fn main() {
    println!("Hello, world!");
    dotenvy::dotenv().ok();

    let addr = env::var("SERVER_ADDR").expect("SERVER_ADDR must be set in .env file");
    let port = env::var("SERVER_PORT").expect("SERVER_PORT must be set in .env file");

    let server = Server::new(addr, port);
    server.start().await.unwrap();
}
