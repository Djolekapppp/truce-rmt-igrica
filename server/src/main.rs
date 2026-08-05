mod server;
mod client_handler;
mod room_manager;


use server::Server;

#[tokio::main]
async fn main() {
    println!("Hello, world!");
    let server = Server::new();
    server.start().await.unwrap();

}
