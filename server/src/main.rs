mod server;
mod client_handler;


use server::Server;

#[tokio::main]
async fn main() {
    println!("Hello, world!");
    let server = Server::new();
    server.start().await.unwrap();

}
