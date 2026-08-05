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
    Response {
        content: String,
    },
    Error {
        message: String,
    },
}


