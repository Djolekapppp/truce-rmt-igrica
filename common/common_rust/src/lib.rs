use serde::{Deserialize, Serialize};

/// Klase (rase) koje igrac moze da izabere u lobiju.
/// Iste vrednosti se koriste kao `class` u cards.json.
pub const CLASSES: [&str; 3] = ["elves", "dwarves", "humans"];

pub fn is_valid_class(class: &str) -> bool {
    CLASSES.contains(&class)
}

/// Ukupan broj epoha u partiji. Dve runde po epohi.
pub const EPOCH_COUNT: u32 = 6;

/// Doba u kome se rasa nalazi tokom jedne epohe.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Age {
    Golden,
    Dark,
    Neutral,
}

// Raspored je fiksan i ponavlja se na svake tri epohe:
//   epoha 1 i 4: vilenjaci zlatno doba, ljudi mracno
//   epoha 2 i 5: ljudi zlatno doba, patuljci mracno
//   epoha 3 i 6: patuljci zlatno doba, vilenjaci mracno
// Rasa koja je u mracnom dobu ulazi u zlatno doba sledece epohe.
const GOLDEN_BY_EPOCH: [&str; 3] = ["elves", "humans", "dwarves"];
const DARK_BY_EPOCH: [&str; 3] = ["humans", "dwarves", "elves"];

/// Rasa u zlatnom dobu date epohe. None pre pocetka partije (epoha 0).
pub fn golden_class(epoch: u32) -> Option<&'static str> {
    if epoch == 0 {
        None
    } else {
        Some(GOLDEN_BY_EPOCH[((epoch - 1) % 3) as usize])
    }
}

/// Rasa u mracnom dobu date epohe. None pre pocetka partije (epoha 0).
pub fn dark_class(epoch: u32) -> Option<&'static str> {
    if epoch == 0 {
        None
    } else {
        Some(DARK_BY_EPOCH[((epoch - 1) % 3) as usize])
    }
}

pub fn age_of(class: &str, epoch: u32) -> Age {
    if golden_class(epoch) == Some(class) {
        Age::Golden
    } else if dark_class(epoch) == Some(class) {
        Age::Dark
    } else {
        Age::Neutral
    }
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
    /// Server -> pojedinacan igrac, na pocetku svake epohe.
    /// Ceo spil iz koga mu se te epohe vuku karte, vec suzen
    /// zlatnim/mracnim dobom, da moze da ga pregleda u klijentu.
    EpochDeck {
        epoch: u32,
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
    /// Kraj partije. `won` je true ako su igraci izdrzali svih
    /// sest epoha, false ako je zadovoljstvo neke rase palo na nulu.
    GameOver {
        won: bool,
    },
    Response {
        content: String,
    },
    Error {
        message: String,
    },
}
