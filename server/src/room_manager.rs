use std::{collections::HashMap, sync::Arc};

use tokio::sync::{Mutex, mpsc};

struct GameState {
    seed: u64,
    turn: u8,
    nature: u8,
    faith: u8,
    science: u8,
}

pub struct Room {
    id: u32,
    players: [Option<u32>; 3], // Array of player ids
    game: GameState,
}

impl Room {
    fn is_empty(&self) -> bool {
        self.players.iter().all(|player| player.is_none())
    }
}

pub struct RoomRequest {
    pub player_id: u32,
    pub message: common::Message,
}

pub struct RoomManager {
    rooms: HashMap<u32, Room>,
    player_room: HashMap<u32, u32>, // Maps player id to room id
    clients: Arc<Mutex<HashMap<u32, mpsc::Sender<common::Message>>>>,
}

impl RoomManager {
    pub fn new(clients: Arc<Mutex<HashMap<u32, mpsc::Sender<common::Message>>>>) -> Self {
        RoomManager {
            rooms: HashMap::new(),
            player_room: HashMap::new(),
            clients,
        }
    }

    pub async fn run(mut self, mut receiver: mpsc::Receiver<RoomRequest>) {
        let mut next_room_id = 0;

        while let Some(request) = receiver.recv().await {
            let player_id = request.player_id;
            match request.message {
                common::Message::Connect { username } => {
                    println!("Player {} connected with username: {}", player_id, username);
                    let _ = self.send_to_player(player_id,
                        common::Message::Response 
                        { content: format!("Welcome, {}!", username) }).await;
                }
                common::Message::CreateRoom => {
                    match self.create_room(next_room_id, player_id) {
                        Ok(_) => {
                            next_room_id += 1;
                            let _ = self.send_to_player(player_id, 
                                common::Message::Response {
                                    content: format!("Room {} created", next_room_id - 1),
                                }).await;
                        }
                        Err(err) => {
                            let _ = self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::JoinRoom { room_id } => {
                    match self.join_room(player_id, room_id) {
                        Ok(_) => {
                            let _ = self.broadcast_to_room(room_id, 
                                common::Message::Response {
                                    content: format!("Player {} joined room {}", player_id, room_id),
                                }).await;
                        }
                        Err(err) => {
                            let _ = self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::LeaveRoom => {
                    match self.leave_room(player_id) {
                        Ok(_) => {
                            let _ = self.send_to_player(player_id, 
                                common::Message::Response {
                                    content: "Left the room".to_string(),
                                }).await;
                        }
                        Err(err) => {
                            let _ = self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::Chat { content } => {
                    todo!("Implement chat logic");
                }
                _ => {}
            }
        }
    }

    async fn send_to_player(&self, client_id: u32, message: common::Message) {
        let clients_lock = self.clients.lock().await;
        if let Some(sender) = clients_lock.get(&client_id) {
            let _ = sender.send(message).await;
        }
    }

    async fn broadcast_to_room(&self, room_id: u32, message: common::Message) {
        let clients_lock = self.clients.lock().await;

        if let Some(room) = self.rooms.get(&room_id) {
            for player_id in room.players.iter().flatten().copied() {
                let sender = clients_lock.get(&player_id);
                if let Some(sender) = sender {
                    sender.send(message.clone()).await.unwrap();
                }
            }
        }
    }


    pub fn create_room(&mut self, room_id: u32, player_id: u32) -> Result<(), String> {
        if let Some(current_room_id) = self.get_player_room(player_id) {
            return Err(format!("Cannot create a room, player already in room {}", current_room_id))
        }

        let game_state = GameState {
            seed: rand::random(),
            turn: 0,
            nature: 0,
            faith: 0,
            science: 0,
        };

        let room = Room {
            id: room_id,
            players: [Some(player_id), None, None],
            game: game_state,
        };
        self.rooms.insert(room_id, room);
        self.player_room.insert(player_id, room_id);
        Ok(())
    }

    fn join_room(&mut self, player_id: u32, room_id: u32) -> Result<(), String> {
        if let Some(current_room_id) = self.get_player_room(player_id) {
            return Err(format!("Player is already in a room with room_id {}", current_room_id));
        }

        if let Some(room) = self.get_room(room_id) {
            if let Some(slot) = room.players.iter_mut().find(|slot| slot.is_none()) {
                *slot = Some(player_id);
                Ok(())
            } else {
                Err("Room is full".to_string())
            }
        } else {
            Err(format!("Cannot find the room with room_id {}", room_id))
        }
    }

    fn leave_room(&mut self, player_id: u32) -> Result<(), String> {
        let room_id = match self.player_room.get(&player_id) {
            Some(&room_id) => room_id,
            None => return Err("Player is not in any room".to_string()),
        };

        if let Some(room) = self.rooms.get_mut(&room_id) {
            if let Some(slot) = room.players.iter_mut().find(|slot| slot.as_ref() == Some(&player_id)) {
                *slot = None;
                self.player_room.remove(&player_id);
            } else {
                return Err("Player not found in the room".to_string());
            }
        } else {
            return Err("Room not found".to_string());
        }
        self.remove_empty_room(room_id);
        Ok(())
    }

    fn remove_empty_room(&mut self, room_id: u32) {
        if let Some(room) = self.rooms.get(&room_id) {
            if room.is_empty() {
                self.rooms.remove(&room_id);
            }
        }
    }

    fn get_room(&mut self, room_id: u32) -> Option<&mut Room> {
        self.rooms.get_mut(&room_id)
    }

    fn get_player_room(&self, player_id: u32) -> Option<u32> {
        self.player_room.get(&player_id).cloned()
    }
}
