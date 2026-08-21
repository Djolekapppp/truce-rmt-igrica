use std::collections::HashMap;

use serde::{Serialize, Deserialize};

const CARDS_FILE_PATH: &str = include_str!("./cards.json");

#[derive(Serialize, Deserialize)]
pub struct Card {
    pub name: String,
    pub epoch: u32,
    pub class: String,
    pub description: String, 
    pub elves: i32, 
    pub dwarves: i32,
    pub humans: i32,
}

pub fn from_json(json: &str) -> HashMap<String, Card> {
    serde_json::from_str(json).unwrap()
}

pub fn load_cards() -> HashMap<String, Card> {
    from_json(CARDS_FILE_PATH)
}
