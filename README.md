# Cognoscent
This is a tool/game for TTRPG parties to play. It uses a custom RPG System called Cognoscent.
## Using
You can build the project using any program that compiles Visual Studio Solution files(SLN). You'll need GodotEngine to run the Client though. The server is just a CLI tool, you can type "help" to show all commands.
## Cognoscent System
The cognoscent system is a custom RPG system, that uses the enhancements provided by the Cognoscent Software. That means that players do not have to manually keep track of inventory items, xp, skills, etc etc. That also means that the combat can be more complex and interesting than "roll dice, hit".
### Combat System
The combat system is not in turns, but instead in ticks. Each action takes an amount of ticks to be executed. How fast you can execute actions is based on your dexterity, among other factors(health, stamina, etc...). When the combat system starts, everyone will start on the same tick unless the characters were surprised, which adds a delay to your action based on dex and perception.
#### Actions
Characters can execute many actions while in combat, but most of them are derived from these:
 - Strike - A strike is any kind of attack. Most strikes will ask you to aim at which part of the enemy's body you want to strike at. Different strikes have different delay ticks, recover ticks, and damage, based on things like player attributes, weapon attributes and the specific strike ability used. If the enemy's perception is high enough(based on how fast the attack is, and your dex), they can predict which part of their body you are attacking.
 - Weave - You'll weave some part of your body out of the way of an attack. If you choose the wrong "area", you'll get hit. If you weave too soon, you'll get hit anyway, the weave has a certain delay based on dex, so if you weave too late, you'll also get hit.
 - Backstep - You'll take a step back, dodging all parts of your body. You also have to time it right, and this one costs more stamina. 
 - Parry - Tries to parry the enemy's attack, if the enemy has more strength, he can break your parry. Has to time it right.
 - Wait - Simply waits X ticks.
