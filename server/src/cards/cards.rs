use std::collections::HashMap;

use serde::{Serialize, Deserialize};

const CARDS_FILE_PATH: &str = include_str!("./cards.json");

#[derive(Serialize, Deserialize)]
pub struct Card {
    pub name: String,
    pub description: String, 
    pub nature: i32, 
    pub faith: i32,
    pub science: i32,
}

pub fn from_json(json: &str) -> HashMap<String, Card> {
    serde_json::from_str(json).unwrap()
}

pub fn load_cards() -> HashMap<String, Card> {
    from_json(CARDS_FILE_PATH)
}
