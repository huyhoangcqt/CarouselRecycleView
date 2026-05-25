using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class CarouselViewRecycleItem
{
    public int itemIndex;
    public int itemPosition;
    public Transform prefab;
}

public interface ICarouselRecycleItem
{
    public void SetupData(object data, int itemIndex, int posIndex, int dataIndex);

    public void OnBeforeSelected(bool isSelected = true);

    public void OnSelected(bool isSelected = true);
}


public class CarouselRecycleItem : MonoBehaviour, ICarouselRecycleItem
{
    [SerializeField] private TextMeshProUGUI text;
    public int ItemIndex;
    public int PosIndex;
    public int DataIndex;

    public virtual void SetupData(object data, int itemIndex, int posIndex, int dataIndex)
    {
        ItemIndex = itemIndex;
        PosIndex = posIndex;
        DataIndex = dataIndex;

        if (data == null)
        {
            Debug.LogError("Data is null"); 
            return;
        }

        text.text = ((SeasonData)data).id.ToString();
    }

    public virtual void OnBeforeSelected(bool isSelected = true)
    {
        // Handle any logic that needs to occur before the item is selected
        // Debug.Log($"Item: {gameObject.name} is about to be {(isSelected ? "selected" : "deselected")}");
    }

    public virtual void OnSelected(bool isSelected = true)
    {
        // Handle item selection logic here
        // Debug.Log($"Item: {gameObject.name} is {(isSelected ? "selected" : "deselected")}");
    }
}

//Temp data:
public class SeasonData
{
    public int id;
    public bool isLocked;
}
