/// Todos los sonidos del juego. Los numeros son fijos: si agregas uno nuevo, ponle un numero que no exista
/// (asi no se desordenan los que ya asignaste en el SoundLibrary).
public enum SfxId
{
    None = -1,

    // UI y menu
    UIHover = 0,
    UIClick = 1,
    UIBack = 2,
    LogoImpact = 10,
    MenuWhoosh = 11,
    PressStart = 12,

    // Jugador
    Footstep = 20,
    Dash = 21,
    Swing = 22,
    ChargeReady = 23,
    ChargedSwing = 24,
    PlayerHit = 25,
    PlayerDeath = 26,
    Respawn = 27,
    Collect = 28,
    WeaponAppear = 29,

    // Enemigos
    EnemyFootstep = 40,
    EnemySwing = 41,
    EnemyHit = 42,
    EnemyDeath = 43,
    EnemySpawn = 44,
    FuseHiss = 45,
    Explosion = 46,
    BirdCharge = 47,
    BirdShoot = 48,
    ProjectileImpact = 49,
    EnemyCharge = 50,

    // Salas y puzzles
    DoorOpen = 60,
    DoorClose = 61,
    GateOpen = 62,
    GateClose = 63,
    ButtonOn = 64,
    ButtonOff = 65,
    PuzzleWrong = 66,
    PuzzleSolved = 67,
    GateBlocked = 69,

    // Trampas
    ArrowFire = 80,
    ArrowImpact = 81,
    SpikesUp = 82,
    SpikesDown = 83,
    FloorCrack = 84,
    FloorFall = 85,
    BreakObject = 86,
    TrapsOff = 87,

    // Jefe
    BossRoar = 100,
    BossSwing = 101,
    BossVolley = 102,
    BossSummon = 103,
    SackThrow = 104,
    SackLand = 105,
    SackCapture = 106,
    SackMash = 107,
    SackEscape = 108,
    BossHit = 109,
    BossStun = 110,
    BossDefeatRumble = 111,
    BossExplosion = 112,
    BossStep = 113,
    Telegraph = 114,

    // Partida
    TimerTick = 120,
    TimeUp = 121,
    SackDrop = 122,
    SackFall = 123,
}

/// Musicas (cada una con su clip en el SoundLibrary).
public enum MusicId
{
    None = 0,
    Menu = 1,
    Gameplay = 2,
    Boss = 3,
    GameOver = 4,
    Win = 5,
}

/// Sonidos de ambiente en bucle (goteo, viento, rumor de la mazmorra...).
public enum AmbienceId
{
    None = 0,
    Dungeon = 1,
    BossRoom = 2,
    Menu = 3,
}
