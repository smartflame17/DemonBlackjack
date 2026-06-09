using System.Collections.Generic;

[System.Serializable]

public sealed class GameData
{
    // Store game info (mostly RunState) here
    public long timestamp;


    public GameData()
    {
        timestamp = System.DateTime.Now.ToFileTime();
    }
}