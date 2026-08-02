use tokio::net::TcpStream;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use rmp_serde::{encode, decode};
use common;

pub struct ClientHandler {
    // Fields for client handling
    client: TcpStream,
}

impl ClientHandler {
    pub fn new(client: TcpStream) -> Self {
        // Initialize client handler
        ClientHandler {
            client,
        } 
    }

    pub async fn handle_client(&mut self) {
        // Handle the client connection
        println!("Handling client: {:?}", self.client.peer_addr());

        loop {
            let message = match self.read_message().await {
                Ok(message) => message,
                Err(e) => {
                    println!("Error reading message: {}", e);

                    let response = common::Message::Response {
                        content: format!("Error: {}", e),
                    };

                    let _ = self.send_message(&response).await;
                    break;
                }
            };

            let response = ClientHandler::logic(message);

            if let Err(e) = self.send_message(&response).await {
                println!("Error sending message: {}", e);
                break;
            }
        }
    }

    // This + Send + Sync are required so the error can be returned from an async function,
    // async functions cannot return a plain error, but must return a Future that resolves to
    // Result, and that Result must be Send and Sync (Send means it is safe to send between
    // threads, and Sync means it is safe to send a reference between threads
    // so it can be used in an async context.
    async fn read_message_len(&mut self) -> Result<usize, Box<dyn std::error::Error + Send + Sync>> {
        let mut len_buffer = [0u8; 4];
        self.client.read_exact(&mut len_buffer).await?;
        Ok(u32::from_le_bytes(len_buffer) as usize)
    }

    async fn read_message(&mut self) -> Result<common::Message, Box<dyn std::error::Error + Send + Sync>> {
        let len = self.read_message_len().await?;

        // Limit the message size to prevent DoS attacks (10 MB in this case)
        if len > 10*1024*1024 {
            return Err("Message too large".into());
        }

        let mut buffer = vec![0u8; len];

        self.client.read_exact(&mut buffer).await?;
        let message = decode::from_slice::<common::Message>(&buffer)?;
        Ok(message)
    }

    async fn send_message(&mut self, message: &common::Message) 
    -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
        let buf = encode::to_vec_named(message)?;
        let len = (buf.len() as u32).to_le_bytes();
        self.client.write_all(&len).await?;
        self.client.write_all(&buf).await?;
        Ok(())
    }

    fn logic(message: common::Message) -> common::Message {
        match message {
            common::Message::Move { x, y } => {
                // Process the request and generate a response
                common::Message::Move { x: x + 1, y: y + 1 }
            },
            common::Message::Chat { content } => {
                // Process the request and generate a response
                common::Message::Response { content: format!("Sent ({:?})", content) }
            },
            _ => {
                println!("Received unsupported message type");
                common::Message::Response { content: "Unsupported message type".to_string() }
            }
        }
    }
}
