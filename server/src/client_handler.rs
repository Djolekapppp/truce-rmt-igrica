use tokio::net::TcpStream;
use tokio::sync::mpsc;
use tokio::{io::AsyncReadExt, io::AsyncWriteExt};
use rmp_serde::{encode, decode};
use common;

use crate::room_manager::RoomRequest;


pub struct ClientHandler {
    // Fields for client handling
    id: u32,
    client: TcpStream,

    receiver: mpsc::Receiver<common::Message>,

    room_sender: mpsc::Sender<RoomRequest>,
}

impl ClientHandler {
    pub fn new(client: TcpStream, id: u32, receiver: mpsc::Receiver<common::Message>, room_sender: mpsc::Sender<RoomRequest>)
        -> Self {
        // Initialize client handler
        ClientHandler {
            id,
            client,
            receiver,
            room_sender,
        } 
    }

    pub async fn handle_client(&mut self) {
        // Handle the client connection
        // println!("Handling client: {:?}", self.client.peer_addr());

        loop {
            tokio::select! {
                result = read_message(&mut self.client) => {
                    match result {
                        Ok(message) => {
                            println!("Received message: {:?}", message);
                            self.room_sender.send(RoomRequest { player_id: self.id, message }).await.unwrap();
                        }
                        Err(e) => {
                            println!("Error reading message: {}", e);
                            break;
                        }
                    }
                }
                Some(message) = self.receiver.recv() => {
                    println!("Sending message from server: {:?}", message);

                    if let Err(e) = send_message(&mut self.client, &message).await {
                        println!("Error sending message: {}", e);
                        break;
                    }
                }
            }
        }
    }


}


    // This + Send + Sync are required so the error can be returned from an async function,
    // async functions cannot return a plain error, but must return a Future that resolves to
    // Result, and that Result must be Send and Sync (Send means it is safe to send between
    // threads, and Sync means it is safe to send a reference between threads
    // so it can be used in an async context.
async fn read_message_len(stream: &mut TcpStream) -> Result<usize, Box<dyn std::error::Error + Send + Sync>> {
    let mut len_buffer = [0u8; 4];
    stream.read_exact(&mut len_buffer).await?;
    Ok(u32::from_le_bytes(len_buffer) as usize)
}

async fn read_message(stream: &mut TcpStream) -> Result<common::Message, Box<dyn std::error::Error + Send + Sync>> {
    let len = read_message_len(stream).await?;

    // Limit the message size to prevent DoS attacks (10 MB in this case)
    if len > 10*1024*1024 {
        return Err("Message too large".into());
    }

    let mut buffer = vec![0u8; len];

    stream.read_exact(&mut buffer).await?;
    let message = decode::from_slice::<common::Message>(&buffer)?;
    Ok(message)
}

async fn send_message(stream: &mut TcpStream, message: &common::Message) 
    -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
        let buf = encode::to_vec_named(message)?;
        let len = (buf.len() as u32).to_le_bytes();
        stream.write_all(&len).await?;
        stream.write_all(&buf).await?;
        stream.flush().await?;
        Ok(())
}
