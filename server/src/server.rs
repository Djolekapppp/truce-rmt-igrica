use std::{sync::Arc, collections::HashMap};
use tokio::{net::TcpListener, sync::{Mutex, mpsc}};
use crate::{client_handler::ClientHandler, room_manager::{RoomManager, RoomRequest}};


pub struct Server {
    clients: Arc<Mutex<HashMap<u32, mpsc::Sender<common::Message>>>>,
    room_sender: mpsc::Sender<RoomRequest>,
}

impl Server {
    pub fn new() -> Self {
        let (room_sender, room_receiver) = mpsc::channel(100);

        let clients = Arc::new(Mutex::new(HashMap::new()));

        let room_manager = RoomManager::new(clients.clone());

        tokio::spawn(async move {
            room_manager.run(room_receiver).await;
        });

        Server {
            clients: clients,
            room_sender,
        }
    }

    pub async fn start(&self) -> Result<(), Box<dyn std::error::Error>> {
        // Start the server and listen for incoming connections
        let listener = TcpListener::bind("127.0.0.1:8080").await?;
        println!("Server is listening on 127.0.0.1:8080");

        let mut next_id = 0;

        loop {
            let (stream, addr) = listener.accept().await?;
            // println!("New client connected: {}", addr);

            let (tx, rx) = mpsc::channel(32);
            let id = next_id;
            let room_sender = self.room_sender.clone();
            next_id += 1;

            //dropuje se mut clients i sa njim lock na njemu
            
            //Klonira se referenca (Arc) ne hashmapa,
            //spawned task mora da poseduje svoje vrednosti
            let clients = self.clients.clone(); 

            // Spawn a new task to handle the client connections
            tokio::spawn(async move {
                let mut handler = 
                    ClientHandler::new(stream, id, rx, room_sender);

                {
                    //lockujemo clients radi thread safety 
                    let mut clients = clients.lock().await; 
                    clients.insert(id, tx);
                }
                handler.handle_client().await;

                let mut clients = clients.lock().await;
                clients.remove(&id);

                println!("Client {} disconnected", id);
            });

        }

    }
}
