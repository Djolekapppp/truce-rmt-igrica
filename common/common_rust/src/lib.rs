use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
#[serde(tag = "kind", content = "payload")]
pub enum Message {
    Connect { username: String },
    CreateRoom,
    JoinRoom {
        room_id: u32,
    },
    LeaveRoom,
    Chat {
        content: String,
    },
    Card {
        name: String,
    },
    Hand {
        cards: Vec<String>,
    },
    GameState {
        seed: u32,
        turn: u32,
        nature: i32,
        faith: i32,
        science: i32,
    },

    Response {
        content: String,
    },
    Error {
        message: String,
    },
}


