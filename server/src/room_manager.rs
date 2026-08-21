use std::{collections::HashMap, sync::{Arc, LazyLock}};
use rand::seq::SliceRandom;
use rand::{rngs::StdRng, SeedableRng};

use tokio::sync::{Mutex, mpsc};

use crate::cards::cards::{ Card, load_cards };

static CARDS: LazyLock<HashMap<String, Card>> = LazyLock::new(|| {
    load_cards()
});

const MODIFIERS: [&str; 3] = ["agitated", "skeptical", "hyped"];
const MODIFIER_VALUES: [f32; 3] = [1.10, 0.8, 1.3];

struct GameState {
    seed: u64,
    turn: u32,
    epoch: u32,
    elves: i32,
    dwarves: i32,
    humans: i32,
}

impl GameState {
    fn modify_value(value: i32, modifier: &str) -> i32 {
        match modifier {
            "agitated" if value < 0 => (value as f32 * MODIFIER_VALUES[0]).round() as i32,
            "skeptical" if value > 0 => (value as f32 * MODIFIER_VALUES[1]).round() as i32,
            "hyped" => (value as f32 * MODIFIER_VALUES[2]).round() as i32,
            "" | "none" => value,
            "agitated" | "skeptical" => value,
            _ => value,
        }
    }

    /// Applies all three players' cards as one atomic round.
    /// `turn` counts completed rounds, not individual card messages.
    fn apply_round(&mut self, cards: [(String, String); 3]) -> Result<(), String> {
        let mut total_elves = 0;
        let mut total_dwarves = 0;
        let mut total_humans = 0;

        for (card_name, modifier) in cards {
            let card = CARDS
                .get(&card_name)
                .ok_or_else(|| format!("Card {} not found", card_name))?;

            total_elves += Self::modify_value(card.elves, &modifier);
            total_dwarves += Self::modify_value(card.dwarves, &modifier);
            total_humans += Self::modify_value(card.humans, &modifier);
        }

        self.elves += total_elves;
        self.dwarves += total_dwarves;
        self.humans += total_humans;

        // One turn = one completed round = all three players have played.
        self.turn += 1;

        // Two rounds per epoch:
        // turn 1-2 -> epoch 1
        // turn 3-4 -> epoch 2
        // turn 5-6 -> epoch 3
        // turn 7-8 -> epoch 4 (stage 2)
        self.epoch = (self.turn - 1) / 2 + 1;

        Ok(())
    }

    /// Sve karte date rase koje mogu da dodju u tekucoj epohi.
    ///
    /// Zlatno i mracno doba se ogledaju upravo ovde: rasa u zlatnom dobu
    /// vuce iz jace polovine svog spila, rasa u mracnom dobu iz slabije,
    /// a treca rasa iz celog spila. Raspored doba je fiksan, vidi
    /// `common::golden_class`.
    ///
    /// Epohe 4-6 koriste spilove epoha 1-3 (`card.epoch + 3 == self.epoch`),
    /// ali su tada na snazi modifikatori.
    fn epoch_pool(&self, class: &str) -> Vec<String> {
        let mut deck: Vec<(&String, &Card)> = CARDS.iter()
            .filter(|(_, card)| card.class == class
                && (card.epoch == self.epoch || card.epoch + 3 == self.epoch))
            .map(|(key, card)| (key, card))
            .collect();

        // Jacina karte = ukupan uticaj na zadovoljstvo sve tri rase.
        // Kljuc ulazi u poredjenje da bi rezultat bio isti bez obzira na
        // redosled iteracije kroz HashMap.
        let strength = |card: &Card| card.elves + card.dwarves + card.humans;
        deck.sort_by(|a, b| strength(a.1).cmp(&strength(b.1)).then(a.0.cmp(b.0)));

        let age = common::age_of(class, self.epoch);
        let half = deck.len() / 2;

        if half > 0 {
            match age {
                common::Age::Dark => deck.truncate(half),
                common::Age::Golden => { deck.drain(..deck.len() - half); }
                common::Age::Neutral => {}
            }
        }

        deck.into_iter().map(|(key, _)| key.clone()).collect()
    }

    fn get_hand(&self, class: String, exclude: &[String]) -> Vec<String> {
        let mut deck: Vec<String> = self.epoch_pool(&class)
            .into_iter()
            .filter(|key| !exclude.contains(key))
            .collect();

        deck.shuffle(&mut StdRng::seed_from_u64(self.seed + self.turn as u64));

        deck.into_iter().take(2).collect()
    }

    fn is_lost(&self) -> bool {
        if self.elves <= 0 || self.dwarves <= 0 || self.humans <= 0 {
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
    // One selected card per player for the current round.
    // The game-state changes only after all three players have selected.
    pending_cards: [Option<(String, String)>; 3],
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
    pub modifier: String,
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
                                        elves: 40,
                                        dwarves: 40,
                                        humans: 40,
                                    };
                                    room.pending_cards = [None, None, None];

                                    // Stage 1 starts without modifiers.
                                    for player in room.players.iter_mut().flatten() {
                                        player.modifier.clear();
                                    }

                                    common::Message::GameState {
                                        seed: actual_seed,
                                        turn: room.game.turn,
                                        epoch: room.game.epoch,
                                        elves: room.game.elves,
                                        dwarves: room.game.dwarves,
                                        humans: room.game.humans,
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
                        self.send_epoch_decks(*room_id).await;
                        self.send_hands(*room_id).await;
                    } 
                }
                common::Message::Card { name } => {
                    let Some(room_id) = self.player_room.get(&player_id).copied() else {
                        self.send_to_player(player_id, common::Message::Error {
                            message: "Game has not started yet".to_string(),
                        }).await;
                        continue;
                    };

                    // Find the player's slot. Slot 0/1/2 corresponds to the three
                    // cooperative players/classes in the room.
                    let player_index = match self.rooms.get(&room_id).and_then(|room| {
                        room.players.iter().position(|p| p.as_ref().map(|p| p.id) == Some(player_id))
                    }) {
                        Some(index) => index,
                        None => {
                            self.send_to_player(player_id, common::Message::Error {
                                message: "Player is not in the room".to_string(),
                            }).await;
                            continue;
                        }
                    };

                    // Ensure stage-2 modifiers exist before any stage-2 card can be played.
                    if self.stage_two_started(room_id) {
                        let modifiers_missing = self.rooms
                            .get(&room_id)
                            .map(|room| room.players.iter().flatten().any(|p| p.modifier.is_empty()))
                            .unwrap_or(false);

                        if modifiers_missing {
                            match self.assign_stage_two_modifiers(room_id) {
                                Ok(assignments) => {
                                    for (target_id, modifier) in assignments {
                                        let index = MODIFIERS.iter().position(|&m| m == modifier).unwrap_or(0);
                                        self.send_to_player(
                                            target_id,
                                            common::Message::Modifier { 
                                            modifier: modifier.clone(),
                                            value: MODIFIER_VALUES[index] },
                                        ).await;
                                    }
                                }
                                Err(err) => {
                                    self.send_to_player(player_id, common::Message::Error { message: err }).await;
                                    continue;
                                }
                            }
                        }
                    }

                    let result = {
                        let Some(room) = self.rooms.get_mut(&room_id) else {
                            self.send_to_player(player_id, common::Message::Error {
                                message: "Room not found".to_string(),
                            }).await;
                            continue;
                        };

                        // A player can only submit once per round.
                        if room.pending_cards[player_index].is_some() {
                            Err("You already selected a card for this round".to_string())
                        } else {
                            let modifier = room.players[player_index]
                                .as_ref()
                                .map(|p| p.modifier.clone())
                                .unwrap_or_default();

                            room.pending_cards[player_index] = Some((name, modifier));

                            // Do not update the game until all three players have selected.
                            if room.pending_cards.iter().all(|card| card.is_some()) {
                                let cards = [
                                    room.pending_cards[0].take().unwrap(),
                                    room.pending_cards[1].take().unwrap(),
                                    room.pending_cards[2].take().unwrap(),
                                ];

                                let was_stage_two = room.game.epoch >= 4;
                                let epoch_before = room.game.epoch;
                                let update_result = room.game.apply_round(cards);

                                match update_result {
                                    Ok(()) => {
                                        let starts_stage_two = !was_stage_two && room.game.epoch >= 4;
                                        let epoch_changed = room.game.epoch != epoch_before;
                                        Ok(Some((
                                            common::Message::GameState {
                                                seed: room.game.seed,
                                                turn: room.game.turn,
                                                epoch: room.game.epoch,
                                                elves: room.game.elves,
                                                dwarves: room.game.dwarves,
                                                humans: room.game.humans,
                                            },
                                            starts_stage_two,
                                            epoch_changed,
                                        )))
                                    }
                                    Err(err) => Err(err),
                                }
                            } else {
                                Ok(None)
                            }
                        }
                    };

                    match result {
                        Ok(Some((game_state_message, starts_stage_two, epoch_changed))) => {
                            self.broadcast_to_room(room_id, game_state_message).await;

                            // Partija se zavrsava na dva nacina: neka rasa je
                            // pala na nulu, ili su izdrzane sve epohe.
                            let outcome = self.rooms.get(&room_id).and_then(|room| {
                                if room.game.is_lost() {
                                    Some(false)
                                } else if room.game.epoch > common::EPOCH_COUNT {
                                    Some(true)
                                } else {
                                    None
                                }
                            });

                            if let Some(won) = outcome {
                                self.broadcast_to_room(room_id,
                                    common::Message::GameOver { won }).await;
                                continue;
                            }

                            // Modifiers are assigned exactly when stage 2 begins.
                            // Each player is told their own modifier privately.
                            if starts_stage_two {
                                if let Ok(assignments) = self.assign_stage_two_modifiers(room_id) {
                                    for (target_id, modifier) in assignments {
                                        let index = MODIFIERS.iter().position(|&m| m == modifier).unwrap_or(0);
                                        self.send_to_player(
                                            target_id,
                                            common::Message::Modifier {
                                                modifier: modifier.clone(),
                                                value: MODIFIER_VALUES[index],
                                            },
                                        ).await;
                                    }
                                }
                            }

                            if epoch_changed {
                                self.send_epoch_decks(room_id).await;
                            }

                            self.send_hands(room_id).await;
                        }
                        Ok(None) => {
                            // The card is locked in, but the state does not change yet.
                        }
                        Err(err) => {
                            self.send_to_player(player_id, common::Message::Error {
                                message: err,
                            }).await;
                        }
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

    fn assign_stage_two_modifiers(&mut self, room_id: u32) -> Result<Vec<(u32, String)>, String> {
        let room = self.rooms.get_mut(&room_id)
            .ok_or_else(|| "Room not found".to_string())?;

        let mut modifiers = MODIFIERS.to_vec();
        modifiers.shuffle(&mut StdRng::seed_from_u64(room.game.seed));

        let mut assignments = Vec::new();
        for (player, modifier) in room.players.iter_mut().flatten().zip(modifiers.into_iter()) {
            player.modifier = modifier.to_string();
            assignments.push((player.id, player.modifier.clone()));
        }

        Ok(assignments)
    }

    fn stage_two_started(&self, room_id: u32) -> bool {
        self.rooms.get(&room_id).map_or(false, |room| room.game.epoch >= 4)
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
            elves: 0,
            dwarves: 0,
            humans: 0,
        };
        let player = self.players.get(&player_id).ok_or_else(|| format!("Player {} not found", player_id))?.clone();

        let room = Room {
            id: room_id,
            players: [Some(player), None, None],
            game: game_state,
            pending_cards: [None, None, None],
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
            player.modifier = String::new();
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
            modifier: "".to_string(),
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

    /// Salje svakom igracu ceo spil iz koga mu se te epohe vuku karte,
    /// da bi mogao da ga pregleda u klijentu.
    async fn send_epoch_decks(&self, room_id: u32) {
        let messages = {
            let Some(room) = self.rooms.get(&room_id) else {
                return;
            };

            room.players.iter().flatten()
                .map(|player| (
                    player.id,
                    common::Message::EpochDeck {
                        epoch: room.game.epoch,
                        cards: room.game.epoch_pool(&player.class),
                    },
                ))
                .collect::<Vec<_>>()
        };

        for (player_id, message) in messages {
            self.send_to_player(player_id, message).await;
        }
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


#[cfg(test)]
mod tests {
    use super::*;

    fn state(epoch: u32) -> GameState {
        GameState { seed: 1, turn: 1, epoch, elves: 40, dwarves: 40, humans: 40 }
    }

    fn strength(key: &str) -> i32 {
        let card = &CARDS[key];
        card.elves + card.dwarves + card.humans
    }

    #[test]
    fn raspored_doba_se_ponavlja_na_svake_tri_epohe() {
        for epoch in 1..=3 {
            assert_eq!(common::golden_class(epoch), common::golden_class(epoch + 3));
            assert_eq!(common::dark_class(epoch), common::dark_class(epoch + 3));
        }

        assert_eq!(common::golden_class(1), Some("elves"));
        assert_eq!(common::dark_class(1), Some("humans"));
        assert_eq!(common::golden_class(2), Some("humans"));
        assert_eq!(common::dark_class(2), Some("dwarves"));
        assert_eq!(common::golden_class(3), Some("dwarves"));
        assert_eq!(common::dark_class(3), Some("elves"));
    }

    #[test]
    fn zlatno_doba_dobija_jacu_polovinu_a_mracno_slabiju() {
        for epoch in 1..=common::EPOCH_COUNT {
            let game = state(epoch);

            for class in common::CLASSES {
                let pool = game.epoch_pool(class);
                let weakest = pool.iter().map(|k| strength(k)).min().unwrap();
                let strongest = pool.iter().map(|k| strength(k)).max().unwrap();

                match common::age_of(class, epoch) {
                    common::Age::Neutral => assert_eq!(pool.len(), 8,
                        "epoha {} {}: mirno doba vuce iz celog spila", epoch, class),
                    _ => assert_eq!(pool.len(), 4,
                        "epoha {} {}: doba suzava spil na polovinu", epoch, class),
                }

                // Zlatna polovina mora biti jaca od mracne za istu rasu.
                if common::age_of(class, epoch) == common::Age::Golden {
                    assert!(weakest >= 0,
                        "epoha {} {}: zlatno doba ne sme da sadrzi kartu jacine {}",
                        epoch, class, weakest);
                }

                if common::age_of(class, epoch) == common::Age::Dark {
                    assert!(strongest <= 0,
                        "epoha {} {}: mracno doba ne sme da sadrzi kartu jacine {}",
                        epoch, class, strongest);
                }
            }
        }
    }

    #[test]
    fn epohe_4_6_koriste_spilove_epoha_1_3() {
        for epoch in 4..=6 {
            for class in common::CLASSES {
                for key in state(epoch).epoch_pool(class) {
                    assert_eq!(CARDS[&key].epoch, epoch - 3,
                        "epoha {} treba da vuce karte epohe {}", epoch, epoch - 3);
                }
            }
        }
    }

    #[test]
    fn ruka_ima_dve_karte_iz_spila_epohe() {
        for epoch in 1..=common::EPOCH_COUNT {
            let game = state(epoch);

            for class in common::CLASSES {
                let pool = game.epoch_pool(class);
                let hand = game.get_hand(class.to_string(), &[]);

                assert_eq!(hand.len(), 2, "epoha {} {}", epoch, class);
                assert_ne!(hand[0], hand[1], "ruka ne sme imati istu kartu dvaput");

                for key in &hand {
                    assert!(pool.contains(key), "karta van spila epohe");
                }
            }
        }
    }

    #[test]
    fn modifikatori_deluju_samo_na_odgovarajuci_znak() {
        // agitated pojacava samo negativne
        assert_eq!(GameState::modify_value(-10, "agitated"), -11);
        assert_eq!(GameState::modify_value(10, "agitated"), 10);

        // skeptical slabi samo pozitivne
        assert_eq!(GameState::modify_value(10, "skeptical"), 8);
        assert_eq!(GameState::modify_value(-10, "skeptical"), -10);

        // hyped pojacava sve
        assert_eq!(GameState::modify_value(10, "hyped"), 13);
        assert_eq!(GameState::modify_value(-10, "hyped"), -13);

        // bez modifikatora nista se ne menja
        assert_eq!(GameState::modify_value(7, ""), 7);
    }
}
