use serde::{Deserialize, Serialize};

/// Klase (rase) koje igrac moze da izabere u lobiju.
/// Iste vrednosti se koriste kao `class` u cards.json.
pub const CLASSES: [&str; 3] = ["elves", "dwarves", "humans"];

pub fn is_valid_class(class: &str) -> bool {
    CLASSES.contains(&class)
}

/// Stanje jednog igraca u lobiju, onako kako ga klijent prikazuje.
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct LobbyPlayer {
    pub id: u32,
    pub username: String,
    pub class: String,
    pub ready: bool,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
#[serde(tag = "kind", content = "payload")]
pub enum Message {
    Connect { username: String },
    /// Server -> klijent, odmah nakon Connect. Klijent tako sazna svoj id
    /// i moze da se prepozna u LobbyState listi.
    Welcome {
        player_id: u32,
    },
    CreateRoom,
    JoinRoom {
        room_id: u32,
    },
    LeaveRoom,
    /// Server -> svi u sobi, na svaku promenu sastava sobe / spremnosti.
    LobbyState {
        room_id: u32,
        players: Vec<LobbyPlayer>,
    },
    Chat {
        content: String,
    },
    Ready {
        class: String,
    },
    Unready,
    StartGame {
        seed: u64
    },
    Card {
        name: String,
    },

    Hand {
        cards: Vec<String>,
    },
    GameState {
        seed: u64,
        turn: u32,
        epoch: u32,
        elves: i32,
        dwarves: i32,
        humans: i32,
    },
    Modifier {
        modifier: String,
        value: f32,
    },
    GameOver,
    Response {
        content: String,
    },
    Error {
        message: String,
    },
}
