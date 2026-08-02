use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug)]
#[serde(tag = "kind", content = "payload")]

pub enum Message {
    Move {
        x: i32,
        y: i32,
    },
    Chat {
        content: String,
    },
    Response {
        content: String,
    },
}


