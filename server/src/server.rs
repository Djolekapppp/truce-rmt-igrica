use std::{sync::Arc, collections::HashMap};
use tokio::{net::TcpListener, sync::Mutex};
use crate::client_handler::ClientHandler;


pub struct Server {
    clients: Arc<Mutex<HashMap<u32, String>>>,
}

impl Server {
    pub fn new() -> Self {
        Server {
            clients: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    pub async fn start(&self) -> Result<(), Box<dyn std::error::Error>> {
        // Start the server and listen for incoming connections
        let listener = TcpListener::bind("127.0.0.1:8080").await?;
        println!("Server is listening on 127.0.0.1:8080");

        let mut next_id = 0;

        loop {
            let (stream, addr) = listener.accept().await?;
            println!("New client connected: {}", addr);

            let id = next_id;
            next_id += 1;

            {
                //lockujemo clients radi thread safety 
                let mut clients = self.clients.lock().await; 
                clients.insert(id, addr.to_string());

                println!("Current clients: {:?}\n", clients);
            }
            //dropuje se mut clients i sa njim lock na njemu
            
            //Klonira se referenca (Arc) ne hashmapa,
            //spawned task mora da poseduje svoje vrednosti
            let clients = self.clients.clone(); 

            // Spawn a new task to handle the client connections
            tokio::spawn(async move {
                let mut handler = 
                    ClientHandler::new(stream);
                handler.handle_client().await;

                let mut clients = clients.lock().await;
                clients.remove(&id);

                println!("Client {} disconnected", id);
            });

        }

    }
}
