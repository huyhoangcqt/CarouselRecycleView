using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class NewBehaviourScript : MonoBehaviour
{
    public CarouselViewRecycle seasonViewCtrl;

    // Start is called before the first frame update
    void Start()
    {
        var currentSeason = 1;
        var firstIndex = currentSeason - 1;

        seasonViewCtrl.SetupDatas<SeasonData>(new List<SeasonData>
        {
            new SeasonData { id = 1, isLocked = false },
            new SeasonData { id = 2, isLocked = true },
            new SeasonData { id = 3, isLocked = true },
            new SeasonData { id = 4, isLocked = true },
            new SeasonData { id = 5, isLocked = true },
        }, 0);
        
        // Snap immediately so item 0 appears in middle on scene start
        seasonViewCtrl.SnapToIndexImmediate(firstIndex);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
