# Playtest tools

Use these controls on the separate ordinary **playtest** account. It has
its own bank wallet. The controls are available on the private loopback
Solo shard and require a grant from its server console.

1. In the server console, enter `playtest provision` once. The console
   reports a private local file containing the account name and generated
   password. Keep that file private. An existing playtest account retains
   its credentials.
2. Open the Britannia Solo launcher, choose **Play**, and select **Playtest**
   if its private profile is installed. Create or select a dummy character.
   You can also sign into the playtest account directly in CrossUO. Keep the
   usual player account for normal progression.
3. With the dummy in the world, enter `playtest grant <exact character name>`
   in the server console. The character receives an in-game message when
   the session is ready.
4. Enter `[Playtest` in CrossUO. The window issues bank checks, sets base
   attributes and individual skills, and gives selected equipment, deeds,
   and materials from a fixed catalog. The issued items go into the
   character's backpack and persist through ordinary saves.
5. Enter `playtest revoke` in the console when finished. A grant lasts up
   to 45 minutes on the same account, character, and login connection.
   Grant again after reconnecting.

The tool supports checks of up to 1,000,000 gold, base stats from 10 to
150, and skill values from 0 to 120 with one decimal place. Enter the skill
name as displayed in the client. Keep the dummy's checks and equipment on
the playtest account so regular progression remains separate.

To disable the console controls, set `playtest.enabled` to false in the
server settings and restart the shard. The command remains absent from
the normal help list. Each action requires the active grant and checks
the loopback connection again.

[Playtest implementation](../../Projects/UOContent/Engines/Playtesting/PlaytestConsole.cs) ·
[Authorization and gump tests](../../Projects/UOContent.Tests/Tests/Engines/PlaytestConsoleTests.cs)
