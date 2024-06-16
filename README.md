# Idem integration into Unity Beamable
This repository contains a simple examples of using `IdemServie` and `IdemMicroservice`

## IdemServiceTest scene
Contains an example of `IdemService` usage.
This is the API that should be used in the most cases: start/stop matchmaking and complete match.
`Delete save` button can be used from a built player to clean `PlayerPrefs` so that the next started instance of the player got another player id and could be used to create a match.

## DirectMicroserviceTest scene
Contains an example of `IdemMicroservice` usage.
This is the lower level API that is used by `IdemService` and should not be used directly.
The example is provided only for information and testing purposes.
