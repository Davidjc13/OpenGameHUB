using OpenGameHUB.Providers.BattleNet;
var path = @"C:\ProgramData\Battle.net\Agent\product.db";
Console.WriteLine($"Exists: {File.Exists(path)}");
if (File.Exists(path)) {
  var entries = BattleNetCatalogReader.ReadLibraryEntriesFromProductDb();
  Console.WriteLine($"ProductDb entries: {entries.Count}");
  foreach (var e in entries.Take(20)) Console.WriteLine($"  {e.ProductId} installed={e.Installed} playable title={e.Title}");
  var merged = BattleNetCatalogReader.ReadMergedCloudEntries();
  Console.WriteLine($"Merged: {merged.Count}");
}
Console.WriteLine($"Launcher: {BattleNetCatalogReader.FindLauncherExecutable()}");
Console.WriteLine($"Available: {BattleNetCatalogReader.IsCloudLibraryAvailable()}");
