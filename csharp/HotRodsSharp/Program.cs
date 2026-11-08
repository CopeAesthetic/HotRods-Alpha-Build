using HotRodsSharp;

SteamMan.Init();
try
{
    GameData.Load();
    using var game = new HotRodsGame();
    game.Run();
}
finally
{
    SteamMan.Shutdown();
}
