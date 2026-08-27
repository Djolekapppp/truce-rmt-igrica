# Čuvari mira
Kooperativna online multiplayer igra za tačno tri igrača, rađena kao projekat iz predmeta Računarske mreže i telekomunikacije.

Svaki igrač je čuvar jedne rase — vilenjaka, patuljaka ili ljudi. Kroz šest epoha igrači zajedno pokušavaju da očuvaju mir u kraljevstvu: svake runde svako iz svoje ruke igra jednu kartu koja utiče na zadovoljstvo sve tri rase. Ako zadovoljstvo bilo koje rase padne na nulu, izbija rat i partija je izgubljena. Ako se izdrži svih šest epoha, mir je očuvan.

Sadržaj
Tehnologije
Struktura projekta
Pokretanje
Kako se igra
Mrežni protokol
Arhitektura
Autori
Tehnologije
Deo	Tehnologija
Klijent	Godot 4, C# (.NET)
Server	Rust, tokio (async runtime)
Transport	TCP, postojana full-duplex veza
Serijalizacija	MessagePack (rmp-serde na serveru, MessagePack-CSharp na klijentu)

Server je autoritativan — drži celokupno stanje igre i jedini donosi odluke o ishodima poteza. Klijent prikazuje stanje koje mu server pošalje.

Struktura projekta
rmt-igrica/
├── server/                 # Rust server
│   ├── src/
│   │   ├── main.rs         # ulazna tačka, čita .env
│   │   ├── server.rs       # TCP listener, prihvatanje konekcija
│   │   ├── client_handler.rs  # jedan task po klijentu
│   │   ├── room_manager.rs    # stanje soba i partija (actor model)
│   │   └── cards/          # učitavanje i obračun karata
│   ├── .env.example
│   └── Cargo.toml
├── common/
│   ├── common_rust/        # definicija protokola (Rust)
│   └── common_cs/          # ista definicija protokola (C#)
└── client/                 # Godot 4 projekat
    ├── scripts/
    │   ├── GameNet.cs      # mrežni sloj, autoload singleton
    │   ├── MainMenu.cs     # povezivanje i izbor sobe
    │   ├── Lobby.cs        # izbor rase, chat, spremnost
    │   ├── Game.cs         # sama partija
    │   ├── Factions.cs / Ages.cs / Modifiers.cs / cards.cs
    └── data/
        └── cards.json      # 72 karte (kopija postoji i na serveru)
Pokretanje
Server

Potreban je Rust (stabilna verzija).

bash
cd server
cp .env.example .env      # podesiti adresu i port
cargo run --release

Podrazumevano server sluša na 127.0.0.1:8080. Adresa i port se čitaju iz .env fajla:

SERVER_ADDR=127.0.0.1
SERVER_PORT=8080
Klijent

Potreban je Godot 4 (.NET/Mono build) i .NET SDK.

Otvoriti folder client/ u Godot editoru.
Sačekati da se C# projekat izbilduje (Build dugme gore desno).
Pokrenuti projekat (F5).

Za lokalno testiranje pokrenuti tri instance klijenta (npr. jedan iz editora, dva eksportovana builda) i povezati ih na isti server.

Kako se igra
Povezivanje — unesu se adresa servera, port i korisničko ime.
Soba — jedan igrač pravi sobu i deli njen ID sa ostalima, koji se pridružuju unosom tog ID-a. Soba prima tačno tri igrača.
Lobi — svaki igrač bira jednu od tri rase (svaka može biti izabrana samo jednom) i potvrđuje spremnost. Postoji i chat. Kada su sva tri mesta popunjena i svi spremni, partija može da počne.
Partija — šest epoha × dve runde. Svake runde igrač dobija ruku od dve karte i bira jednu. Runda se razrešava tek kada sva tri igrača odigraju: efekti se sabiraju i primenjuju na zadovoljstvo rasa.
Kraj — poraz ako bilo koja rasa padne na nulu, pobeda ako se izdrži svih dvanaest rundi. Sa ekrana kraja partije moguć je revanš (povratak u lobi) ili izlazak.
Rase
Rasa	Uloga	Resurs
Vilenjaci	čuvari šuma i živog sveta	Priroda
Patuljci	kovači, graditelji, izumitelji	Nauka
Ljudi	hramovi, zakoni i zavet	Vera

Svaka rasa počinje sa zadovoljstvom 40.

Doba

U svakoj epohi jedna rasa je u zlatnom, jedna u mračnom, a jedna u mirnom dobu; raspored se ponavlja na svake tri epohe. Zlatno doba znači izvlačenje iz jače polovine špila, mračno iz slabije, mirno iz celog špila. Rasa koja je bila u mračnom dobu ulazi u zlatno sledeće epohe.

Karte

72 karte definisane u cards.json — po 8 karata za svaku rasu u epohama 1–3; epohe 4–6 ponovo koriste iste špilove, ali uz modifikatore. Svaka karta ima ime, opis događaja i tri broja (promena zadovoljstva vilenjaka, patuljaka i ljudi):

json
"elv1_vD": {
    "name": "Great Victory over the Dwarves",
    "epoch": 1,
    "class": "elves",
    "description": "A great warrior of the elves has defeated the dwarves...",
    "elves": 30,
    "dwarves": -10,
    "humans": 0
}

Ruka se izvlači determinističkim mešanjem špila na osnovu seed-a partije; izvučene karte se ne vraćaju u špil do kraja epohe.

Modifikatori (druga faza)

Od četvrte epohe svaki igrač nasumično dobija jedan modifikator koji menja efekte njegovih karata:

Modifikator	Efekat
Uznemireni (agitated)	negativni efekti ×1.10, pozitivni nepromenjeni
Skeptični (skeptical)	pozitivni efekti ×0.8, negativni nepromenjeni
Ushićeni (hyped)	svi efekti ×1.3

Obračun radi server; klijent istu formulu ponavlja samo radi prikaza (osnovna + modifikovana vrednost na karti).

Mrežni protokol

Komunikacija ide preko jedne postojane TCP veze koja ostaje otvorena tokom cele sesije. Veza je full-duplex, pa server može da obavesti klijenta o promeni stanja čim se ona desi.

Framing

Pošto je TCP tok bajtova bez granica poruka, uveden je sopstveni okvir:

[ 4 bajta dužine (u32, little-endian) ][ telo poruke (MessagePack) ]

Primalac čita 4 bajta, sazna dužinu, pročita tačno toliko bajtova i deserijalizuje ih. Server ograničava dužinu poruke na 10 MB kao zaštitu od DoS napada.

Poruke

Protokol je definisan kao Rust enum sa kind / payload poljima, i ogledalno kao C# klase koje implementiraju IGameMessage:

rust
#[serde(tag = "kind", content = "payload")]
pub enum Message {
    Connect { username: String },
    Welcome { player_id: u32 },
    CreateRoom,
    JoinRoom { room_id: u32 },
    LeaveRoom,
    LobbyState { room_id: u32, players: Vec<LobbyPlayer> },
    Chat { content: String },
    Ready { class: String },
    Unready,
    StartGame { seed: u64 },
    Card { name: String },
    Hand { cards: Vec<String> },
    EpochDeck { epoch: u32, cards: Vec<String>, drawable: Vec<String> },
    GameState { seed: u64, turn: u32, epoch: u32,
                elves: i32, dwarves: i32, humans: i32 },
    Modifier { modifier: String, value: f32 },
    GameOver { won: bool },
    Response { content: String },
    Error { message: String },
}
Arhitektura
Server
Glavna petlja sluša na TCP portu i za svakog klijenta dodeljuje ID i pokreće poseban tokio task — ClientHandler.
ClientHandler koristi tokio::select! da istovremeno čeka poruke sa soketa i poruke iz internog kanala ka tom klijentu.
RoomManager je zaseban task po uzoru na actor model: svi handler-i mu šalju zahteve kroz jedan mpsc kanal, pa on obrađuje zahteve jedan po jedan. Time stanju igre uvek pristupa samo jedna nit — nema race condition-a ni zaključavanja stanja partije.
Jedina deljena struktura je mapa player_id → kanal, zaštićena sa Arc<Mutex<...>>, preko koje se šalju odgovori pojedincu ili broadcast celoj sobi.
Klijent
GameNet je Godot autoload singleton — živi tokom celog rada aplikacije, nezavisno od aktivne scene. Sve scene preko njega šalju poruke i pretplaćuju se na evente (LobbyUpdated, GameStateUpdated, HandUpdated, GameOverReceived…).
Poruke se primaju na pozadinskom tasku i ubacuju u ConcurrentQueue, a red se prazni u _Process na glavnoj niti — pa handler-i smeju bezbedno da menjaju UI.
GameNet kešira poslednje poznato stanje (lobi, partija, ruka, modifikator, špil epohe), tako da poruke pristigle tokom promene scene ne propadaju.
Autori
Veljko Ristić
Dušan Ristić
Aleksandar Đorđević