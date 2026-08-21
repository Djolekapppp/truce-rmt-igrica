use std::{collections::HashMap, sync::{Arc, LazyLock}};
use rand::seq::SliceRandom;
use rand::{rngs::StdRng, SeedableRng};

use tokio::sync::{Mutex, mpsc};

use crate::cards::cards::{ Card, load_cards };

static CARDS: LazyLock<HashMap<String, Card>> = LazyLock::new(|| {
    load_cards()
});

struct GameState {
    seed: u64,
    turn: u32,
    epoch: u32,
    nature: i32,
    faith: i32,
    science: i32,
}

impl GameState {
    fn update(&mut self, card: String) -> Result<(), String> {
        if let Some(card) = CARDS.get(&card) {
            self.nature += card.nature;
            self.faith += card.faith;
            self.science += card.science;
            self.turn += 1;
            self.epoch = (self.turn-1) / 2 + 1;
            Ok(())
        } else {
            Err(format!("Card {} not found", card))
        }
    }

    fn get_hand(&self, class: String, exclude: &[String]) -> Vec<String> {
        let mut deck: Vec<&String> = CARDS.iter()
            .filter(|(key, card)| *card.class == class
                && !exclude.contains(key)
                && card.epoch == self.epoch) 
               
            .map(|(key, _)| key).collect();

        deck.shuffle(&mut StdRng::seed_from_u64(self.seed + self.turn as u64));

        deck.into_iter().take(2).cloned().collect()
    }

    fn is_lost(&self) -> bool {
        if self.nature < 0 || self.faith < 0 || self.science < 0 {
            true
        } else {
            false
        }
    }
}

pub struct Room {
    id: u32,
    players: [Option<Player>; 3], 
    game: GameState,
}

impl Room {
    fn is_empty(&self) -> bool {
        self.players.iter().all(|player| player.is_none())
    }

    fn is_ready(&self) -> bool {
        self.players.iter().all(|player| player.as_ref().map_or(false, |p| p.ready))
    }
}

pub struct RoomRequest {
    pub player_id: u32,
    pub message: common::Message,
}

#[derive(Debug, Clone)]
pub struct Player {
    pub id: u32, 
    pub username: String,
    pub class: String,
    pub hand: Vec<String>,
    pub ready: bool,
}


pub struct RoomManager {
    rooms: HashMap<u32, Room>,
    players: HashMap<u32, Player>,
    player_room: HashMap<u32, u32>, // Maps player id to room id
    clients: Arc<Mutex<HashMap<u32, mpsc::Sender<common::Message>>>>,
}

impl RoomManager {
    pub fn new(clients: Arc<Mutex<HashMap<u32, mpsc::Sender<common::Message>>>>) -> Self {
        RoomManager {
            rooms: HashMap::new(),
            player_room: HashMap::new(),
            players: HashMap::new(),
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
                    self.create_player(player_id, username.clone());
                    self.send_to_player(player_id,
                        common::Message::Welcome { player_id }).await;
                    self.send_to_player(player_id,
                        common::Message::Response 
                        { content: format!("Welcome, {}!", username) }).await;
                }
                common::Message::CreateRoom => {
                    match self.create_room(next_room_id, player_id) {
                        Ok(_) => {
                            next_room_id += 1;
                            let room_id = next_room_id - 1;
                            self.send_to_player(player_id, 
                                common::Message::Response {
                                    content: format!("Room {} created", room_id),
                                }).await;
                            self.broadcast_lobby_state(room_id).await;
                        }
                        Err(err) => {
                            self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::JoinRoom { room_id } => {
                    match self.join_room(player_id, room_id) {
                        Ok(_) => {
                            self.broadcast_to_room(room_id, 
                                common::Message::Response {
                                    content: format!("Player {} joined room {}", player_id, room_id),
                                }).await;
                            self.broadcast_lobby_state(room_id).await;
                        }
                        Err(err) => {
                            self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::LeaveRoom => {
                    match self.leave_room(player_id) {
                        Ok(room_id) => {
                            self.send_to_player(player_id, 
                                common::Message::Response {
                                    content: "Left the room".to_string(),
                                }).await;
                            self.broadcast_lobby_state(room_id).await;
                        }
                        Err(err) => {
                            self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::Ready { class } => {
                    match self.set_ready(player_id, class) {
                        Ok(room_id) => {
                            self.broadcast_lobby_state(room_id).await;
                        }
                        Err(err) => {
                            self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::Unready => {
                    match self.set_unready(player_id) {
                        Ok(room_id) => {
                            self.broadcast_lobby_state(room_id).await;
                        }
                        Err(err) => {
                            self.send_to_player(player_id, 
                                common::Message::Error {
                                    message: err,
                                }).await;
                        }
                    }
                }
                common::Message::StartGame { seed }=> {
                    if let Some(room_id) = self.player_room.get(&player_id) {
                        let game_state_message = {
                            if let Some(room) = self.rooms.get_mut(room_id) {

                                if room.is_ready() {
                                    let actual_seed = if seed != 0 { seed } else { rand::random::<u64>() };

                                    room.game = GameState {
                                        seed: actual_seed,
                                        turn: 1,
                                        epoch: 1,
                                        nature: 10,
                                        faith: 10,
                                        science: 10,
                                    };

                                    common::Message::GameState {
                                        seed: actual_seed,
                                        turn: room.game.turn,
                                        epoch: room.game.epoch,
                                        nature: room.game.nature,
                                        faith: room.game.faith,
                                        science: room.game.science,
                                    }
                                } else {
                                    self.send_to_player(player_id, 
                                        common::Message::Error {
                                            message: "Not all players are ready".to_string(),
                                        }).await;
                                    continue;
                                }
                            } else {
                                self.send_to_player(player_id, 
                                    common::Message::Error {
                                        message: "Room not found".to_string(),
                                    }).await;
                                continue;
                            }
                        };
                        self.broadcast_to_room(*room_id, 
                            game_state_message).await;
                        self.send_hands(*room_id).await;
                    } 
                }
                common::Message::Card { name } => {
                    if let Some(room_id) = self.player_room.get(&player_id) {
                        let game_state_message = {
                            if let Some(room) = self.rooms.get_mut(room_id) {
                                match room.game.update(name) {
                                    Ok(_) => {
                                        common::Message::GameState {
                                            seed: room.game.seed,
                                            turn: room.game.turn,
                                            epoch: room.game.epoch,
                                            nature: room.game.nature,
                                            faith: room.game.faith,
                                            science: room.game.science,
                                        }
                                    }
                                    Err(err) => {
                                        self.send_to_player(player_id, 
                                            common::Message::Error {
                                                message: err,
                                            }).await;
                                        continue;
                                    }
                                }

                            } else {
                                self.send_to_player(player_id, 
                                    common::Message::Error {
                                        message: "Room not found".to_string(),
                                    }).await;
                                continue;
                            }
                        };
                        if let Some(room) = self.rooms.get_mut(room_id) {
                            if room.game.is_lost() {
                                self.broadcast_to_room(*room_id, 
                                    common::Message::GameOver).await;
                            }
                        }

                        self.broadcast_to_room(*room_id, 
                            game_state_message).await;

                        self.send_hands(*room_id).await;

                    } else {
                        self.send_to_player(player_id, 
                            common::Message::Error {
                                message: "Game has not started yet".to_string(),
                            }).await;
                    }
                }

                common::Message::Chat { content } => {
                    if let Some(room_id) = self.player_room.get(&player_id).copied() {
                        let username = self.players.get(&player_id)
                            .map(|p| p.username.clone())
                            .unwrap_or_else(|| format!("Player {}", player_id));

                        self.broadcast_to_room(room_id, 
                            common::Message::Chat {
                                content: format!("{}: {}", username, content),
                            }).await;
                    } else {
                        self.send_to_player(player_id, 
                            common::Message::Error {
                                message: "You are not in a room".to_string(),
                            }).await;
                    }
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
            for player_id in room.players.iter().flatten().map(|p| p.id) {
                let sender = clients_lock.get(&player_id);
                if let Some(sender) = sender {
                    if let Err(e) = sender.send(message.clone()).await {
                        eprintln!("Failed to send message to player {}: {}", player_id, e);
                    }
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
            epoch: 0,
            nature: 0,
            faith: 0,
            science: 0,
        };
        let player = self.players.get(&player_id).ok_or_else(|| format!("Player {} not found", player_id))?.clone();

        let room = Room {
            id: room_id,
            players: [Some(player), None, None],
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
        let player = self.get_player(player_id).ok_or_else(|| format!("Player with id {} not found", player_id))?.clone();

        if let Some(room) = self.get_room(room_id) {
            if let Some(slot) = room.players.iter_mut().find(|slot| slot.is_none()) {
                *slot = Some(player);
                self.player_room.insert(player_id, room_id);
                Ok(())
            } else {
                Err("Room is full".to_string())
            }
        } else {
            Err(format!("Cannot find the room with room_id {}", room_id))
        }
    }

    fn leave_room(&mut self, player_id: u32) -> Result<u32, String> {
        let room_id = match self.player_room.get(&player_id) {
            Some(&room_id) => room_id,
            None => return Err("Player is not in any room".to_string()),
        };

        if let Some(room) = self.rooms.get_mut(&room_id) {
            if let Some(slot) = room.players.iter_mut().find(|slot| slot.as_ref().map(|p| p.id) == Some(player_id)) {
                *slot = None;
                self.player_room.remove(&player_id);
            } else {
                return Err("Player not found in the room".to_string());
            }
        } else {
            return Err("Room not found".to_string());
        }

        if let Some(player) = self.players.get_mut(&player_id) {
            player.class = String::new();
            player.ready = false;
            player.hand = vec![String::new(), String::new()];
        }

        self.remove_empty_room(room_id);
        Ok(room_id)
    }

    /// Postavlja klasu i spremnost igraca.
    ///
    /// Vazno: izvor istine za lobi je `room.players`, jer `Room::is_ready`
    /// i `send_hands` citaju odatle. Ranije se azurirao samo `self.players`,
    /// pa je `is_ready()` uvek vracao false i StartGame nikad nije prolazio.
    fn set_ready(&mut self, player_id: u32, class: String) -> Result<u32, String> {
        let class = class.trim().to_lowercase();

        if !common::is_valid_class(&class) {
            return Err(format!("Unknown class: {}", class));
        }

        let room_id = self.player_room.get(&player_id).copied()
            .ok_or_else(|| "You are not in a room".to_string())?;

        let room = self.rooms.get_mut(&room_id)
            .ok_or_else(|| "Room not found".to_string())?;

        let taken = room.players.iter().flatten()
            .any(|p| p.id != player_id && p.class == class);

        if taken {
            return Err(format!("Class {} is already taken", class));
        }

        let slot = room.players.iter_mut().flatten()
            .find(|p| p.id == player_id)
            .ok_or_else(|| "Player not found in the room".to_string())?;

        slot.class = class.clone();
        slot.ready = true;

        if let Some(player) = self.players.get_mut(&player_id) {
            player.class = class;
            player.ready = true;
        }

        Ok(room_id)
    }

    fn set_unready(&mut self, player_id: u32) -> Result<u32, String> {
        let room_id = self.player_room.get(&player_id).copied()
            .ok_or_else(|| "You are not in a room".to_string())?;

        let room = self.rooms.get_mut(&room_id)
            .ok_or_else(|| "Room not found".to_string())?;

        let slot = room.players.iter_mut().flatten()
            .find(|p| p.id == player_id)
            .ok_or_else(|| "Player not found in the room".to_string())?;

        slot.class = String::new();
        slot.ready = false;

        if let Some(player) = self.players.get_mut(&player_id) {
            player.class = String::new();
            player.ready = false;
        }

        Ok(room_id)
    }

    /// Snimak lobija koji klijent koristi da iscrta slotove igraca.
    fn lobby_state(&self, room_id: u32) -> Option<common::Message> {
        let room = self.rooms.get(&room_id)?;

        let players = room.players.iter().flatten()
            .map(|p| common::LobbyPlayer {
                id: p.id,
                username: p.username.clone(),
                class: p.class.clone(),
                ready: p.ready,
            })
            .collect();

        Some(common::Message::LobbyState { room_id, players })
    }

    async fn broadcast_lobby_state(&self, room_id: u32) {
        if let Some(message) = self.lobby_state(room_id) {
            self.broadcast_to_room(room_id, message).await;
        }
    }

    fn remove_empty_room(&mut self, room_id: u32) {
        if let Some(room) = self.rooms.get(&room_id) {
            if room.is_empty() {
                self.rooms.remove(&room_id);
            }
        }
    }

    fn create_player(&mut self, player_id: u32, username: String) -> u32 {
        let player = Player {
            id: player_id,
            username: username,
            class: "".to_string(),
            hand: vec!["".to_string(),"".to_string()],
            ready: false,
        };
        self.players.insert(player_id, player);
        player_id
    }

    fn get_player(&self, player_id: u32) -> Option<&Player> {
        self.players.get(&player_id)
    }

    fn get_room(&mut self, room_id: u32) -> Option<&mut Room> {
        self.rooms.get_mut(&room_id)
    }

    fn get_player_room(&self, player_id: u32) -> Option<u32> {
        self.player_room.get(&player_id).cloned()
    }

    async fn send_hands(&mut self, room_id: u32) {
        let messages = {
            let Some(room) = self.rooms.get_mut(&room_id) else {
                return;
            };

            let mut messages = Vec::new();

            for player in room.players.iter_mut().flatten() {
                let cards = room.game.get_hand(player.class.clone(), &player.hand);

                player.hand = cards.clone();

                messages.push((
                        player.id,
                        common::Message::Hand {
                            cards,
                        },
                ));

            }
            messages
        };

        for (player_id, message) in messages {
            self.send_to_player(player_id, message).await;
        }
    }
}
