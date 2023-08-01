**Scaling Crowdedness** is a simple mod that increases the number of Town NPCs that can be near each other before the crowding happiness penalty starts. The increase is based on how many Town NPCs are loaded. This means you can have more Town NPCs living near each other without having prices raised.

Vanilla behavior:
-  \>= 4 npcsWithinHouse causes prices to be multiplied by 1.05
-  4-5 npcsWithinHouse shows the DislikeCrowded text.
-  \>= 6 npcsWithinHouse shows the HateCrowded text.
-  There are 26 Town NPCs in vanilla.

This mod changes it so after 30 Town NPCs, every 10 additional Town NPCs loaded will increase the thresholds by 1.

Example: 30 Town NPCs loaded:
-  \>= 5 npcsWithinHouse causes prices to be multiplied by 1.05
-  6-7 npcsWithinHouse shows the DislikeCrowded text.
-  \>= 8 npcsWithinHouse shows the HateCrowded text.

Example: 40 Town NPCs loaded:
-  \>= 6 npcsWithinHouse causes prices to be multiplied by 1.05
-  7-8 npcsWithinHouse shows the DislikeCrowded text.
-  \>= 9 npcsWithinHouse shows the HateCrowded text.

This mod has a few commands to show information:

### `/sc`

#### TownNPCChat <true/false>
    Town NPCs will say how many other Town NPCs are near them in chat.
    Example: `/sc TownNPCChat true`

#### EnterWorld <true/false>
    The new thresholds will be displayed in chat when entering a world.
    Example: `/sc EnterWorld true`

#### GetThreshold
    Displays the current threshold in chat.
    Example: `/sc GetThreshold`
