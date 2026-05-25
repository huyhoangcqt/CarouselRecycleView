using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using DG.Tweening;

public partial class CarouselViewRecycle : MonoBehaviour
{
    [Header("Fields")]
    //Fields:
    public Transform content; //itemRoot
    public Transform positionRoot;
    public CarouselRecycleItem itemPrefab;
    public Transform hiddenItemRoot;
    
    [Header("Hidden Position")]
    [SerializeField] private Transform position_hidden_left;
    [SerializeField] private Transform position_hidden_right;

    [Header("Navigation Button")]
    public Button btnLeft;
    public Button btnRight;

    
    //Properties:
    public int ItemCount => items.Count; //= PositionCount (item prefabs count)
    public int PositionCount => positionRoot.childCount; //ITEM_COUNT (prefab)
    public int FirstPosIndex => 0;
    public int LastPosIndex => positions.Count - 1;
    public int MiddlePosIndex => positions.Count / 2;
    public const int POS_HIDDEN_LEFT = -1;
    public int POS_HIDDEN_RIGHT => PositionCount;
    public int HALF_POSITION_COUNT => PositionCount / 2;
    public const int MIN_ITEM_COUNT = 5;

    //Prameters:
    private CarouselRecycleItem hiddenItem;
    private List<CarouselRecycleItem> items = new List<CarouselRecycleItem>();
    // private List<CarouselItem> items = new List<CarouselItem>();
    private List<Vector3> positions = new List<Vector3>();
    private Dictionary<int, int> itemToPosIndex = new Dictionary<int, int>();
    private List<object> currentDatas = new List<object>();
    public int CurrentIndex;
    private bool isDuringSetup = false;
    private bool isSetupComplete = false;
    private int currentDataIndex = 0; //NOTE: currentIndex is the index of item prefab, and also the index of data, because we have same count of item prefab and data, and 1 by 1 mapping
    public int DataCount = 0;
    private Dictionary<int, int> itemToDataIndex = new Dictionary<int, int>(); //mapping item index to data index

    [System.Serializable]
    public class ItemData
    {
        public int Index;//item index
        public int PosIndex;//position index
        public int DataIndex;//data index
        public bool isHidden;
    }
    [SerializeField] private List<ItemData> debugItemDataList = new List<ItemData>();


    //NOTE: 
    //- index: item index, posIndex: position index
    //- itemToPosIndex: mapping item index to position index

    #region Main:

    private void Awake()
    {
        SetupPosition();
        SetupButtons();
        SetupItems();  
        isSetupComplete = true; 

        //InitMappingData:
        CurrentIndex = 0;
        var initialDataIndexMapping = CaculateDataIndexMapping(5, CurrentIndex);
        MappingDataIndex(initialDataIndexMapping);

        for (int i = 0; i < ItemCount; i++)
        {
            itemToPosIndex[i] = i;
        }
        
        UpdateDebugItemDataList();
    }
    private void UpdateDebugItemDataList()
    {
        for (int i = 0; i < ItemCount; i++)
        {
            var itemData = debugItemDataList.FirstOrDefault(x => x.Index == i);
            if (itemData == null)
            {
                itemData = new ItemData { Index = i };
                debugItemDataList.Add(itemData);
            }
            itemData.PosIndex = itemToPosIndex.ContainsKey(i) ? itemToPosIndex[i] : -999;
            itemData.DataIndex = itemToDataIndex.ContainsKey(i) ? itemToDataIndex[i] : -999;
            itemData.isHidden = (hiddenItem != null && items[i] == hiddenItem);
        }
    }

    private void Start()
    {
    }

    public void SetupDatas<T>(List<T> datas, int startIndex) where T : class
    {    
        currentDataIndex = startIndex;
        DataCount = datas.Count;
        currentDatas.Clear();
        currentDatas.AddRange(datas.Cast<object>());
        var dataIndexMapping = CaculateDataIndexMapping(datas.Count, CurrentIndex);
        MappingDataIndex(dataIndexMapping);
        // var mappingDataIndex = CaculateDataIndexMapping(datas.Count, CurrentIndex, MiddlePosIndex);

        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var dataIndex = itemToDataIndex[itemIndex];
            var item = items[itemIndex];
            item.SetupData(datas[dataIndex], itemIndex, itemToPosIndex[itemIndex], dataIndex);
        }

        UpdateDebugItemDataList();
    }

    private Dictionary<int, int> CaculateDataIndexMapping(int dataCount, int selectIndex)
    {
        var result = new Dictionary<int, int>();
        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var gapIndex = itemIndex - selectIndex;
            var dataIndex = (currentDataIndex + gapIndex) % dataCount;
            if (dataIndex < 0)
            {
                dataIndex += dataCount;
            }
            if (dataIndex >= DataCount)
            {
                dataIndex -= DataCount;
            }
            result[itemIndex] = dataIndex;
        }
        return result;
    }

    #endregion Main!!!

    #region Task - SetupButtons:

    private void SetupButtons()
    {
        btnLeft.onClick.AddListener(() =>
        {
            MoveLeft();
        });

        btnRight.onClick.AddListener(() =>
        {
            MoveRight();
        });
    }

    public void MoveLeft()
    {
        var newIndex = CurrentIndex - 1;
        if (newIndex < 0)
        {
            newIndex += ItemCount;
        }

        var newDataIndex = currentDataIndex - 1;
        if (newDataIndex < 0)
        {
            newDataIndex += DataCount;
        }
        currentDataIndex = newDataIndex;

        Debug.Log($"Move Left: newIndex: {newIndex}, newDataIndex: {newDataIndex}");

        if (IsValidIndex(newIndex))
        {
            MoveToIndex(newIndex, direction: -1).Forget();
        }
    }

    public void MoveRight()
    {
        var newIndex = CurrentIndex + 1;
        if (newIndex >= ItemCount)
        {
            newIndex -= ItemCount;
        }

        var newDataIndex = currentDataIndex + 1;
        if (newDataIndex >= DataCount)
        {
            newDataIndex -= DataCount;
        }
        currentDataIndex = newDataIndex;
        Debug.Log($"Move Right: newIndex: {newIndex}, newDataIndex: {newDataIndex}");

        if (IsValidIndex(newIndex))
        {
            MoveToIndex(newIndex, direction: 1).Forget();
        }
    }
    #endregion Task - SetupButtons!!!

    #region Task - Move:
    /// <summary>
    /// MoveToIndex: Move Item: {index} to middle position, and other items move accordingly
    /// </summary>
    /// <param name="index"></param>
    /// <param name="direction"></param>
    /// <param name="duration"></param>
    public async UniTask MoveToIndex(int index, int direction = 0, float duration = 0.5f)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogError($"Index: {index} is out of range");
            return;
        }
        await WaitForReady();

        Debug.Log("Select index: " + index);

        var itemIndexToHide = FindItemToHide(direction);

        CurrentIndex = index;
        var positionMapping = CaculatePositionMapping(index, MiddlePosIndex, direction);
        MappingPosition(positionMapping);
        Debug.Log($"Move to index: {index}, direction: {direction}");
        
        // Phase 1 & 2: Move all items + Move hidden item in parallel
        await UniTask.WhenAll(
            Move(duration, direction, itemIndexToHide)
            // MoveHiddenItem(direction, duration)
        );
        
        // Phase 3: Finalize swap
        ReAsignItemDataIndex();
        UpdateDebugItemDataList();
        FinalizeMoveAndSwap(itemIndexToHide,direction);
    }

    private int FindItemToHide(int direction)
    {
        if (direction == 1)  // MoveRight (Item shift to left)
        {
            return FindItemByPosIndex(FirstPosIndex);
        }
        else if (direction == -1)  // MoveLeft (Item shift to right)
        {
            return FindItemByPosIndex(LastPosIndex);
        }
        return -1; // Invalid direction
    }

    private void ReAsignItemDataIndex()
    {
        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var dataIndex = itemToDataIndex[itemIndex];
            var item = items[itemIndex];
            item.SetupData(currentDatas[dataIndex], itemIndex, itemToPosIndex[itemIndex], dataIndex);
        }
    }

    /// <summary>
    /// Caculate to mapping item index to position index
    /// </summary>
    /// <param name="selectIndex"></param>
    /// <param name="middlePosIndex"></param>
    /// <returns></returns>
    private Dictionary<int, int> CaculatePositionMapping(int selectIndex, int middlePosIndex, int direction)
    {
        var result = new Dictionary<int, int>();

        var index_factor = direction * (-1);

        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var oldPosIndex = itemToPosIndex[itemIndex];
            var posIndex = oldPosIndex + index_factor;
            
            if (posIndex < POS_HIDDEN_LEFT)
            {
                posIndex += PositionCount;
            }
            else if (posIndex > POS_HIDDEN_RIGHT)
            {
                posIndex -= PositionCount;
            }
            
            result[itemIndex] = posIndex;
        }
        return result;
    }
    
    /// <summary>
    /// Mapping item index to position index
    /// </summary>
    /// <param name="itemToPosIndex"></param>
    private void MappingPosition(Dictionary<int, int> itemToPosIndex)
    {
        foreach (var kvp in itemToPosIndex)
        {
            var itemIndex = kvp.Key;
            var posIndex = kvp.Value;
            this.itemToPosIndex[itemIndex] = posIndex;
        }
    }

    private void MappingDataIndex(Dictionary<int, int> itemToDataIndex)
    {
        foreach (var kvp in itemToDataIndex)
        {
            var itemIndex = kvp.Key;
            var dataIndex = kvp.Value;
            this.itemToDataIndex[itemIndex] = dataIndex;
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < ItemCount;
    }

    private async UniTask MoveHiddenItem(int direction, float duration)
    {
        if (hiddenItem == null || ItemCount == 0 || currentDatas.Count == 0 || DataCount == 0)
        {
            return;
        }

        // Determine which item will go to hidden and where hidden item will go
        int itemToHideIndex = -1;
        Vector3 hiddenFromPos, hiddenIntermediatePos = Vector3.zero;
        int visiblePosIndex = -1;

        if (direction == 1)  // MoveRight (Item shift to left)
        {
            // Item trái cùng sẽ move vào hidden left
            itemToHideIndex = (CurrentIndex - MiddlePosIndex + ItemCount) % ItemCount;
            hiddenFromPos = position_hidden_left.position;
            hiddenIntermediatePos = position_hidden_right.position;
            visiblePosIndex = LastPosIndex;
        }
        else if (direction == -1)  // MoveLeft (Item shift to right)
        {
            // Item phải cùng sẽ move vào hidden right
            itemToHideIndex = (CurrentIndex + MiddlePosIndex) % ItemCount;
            hiddenFromPos = position_hidden_right.position;
            hiddenIntermediatePos = position_hidden_left.position;
            visiblePosIndex = FirstPosIndex;
        }

        if (itemToHideIndex < 0 || itemToHideIndex >= ItemCount || visiblePosIndex < 0)
        {
            return;
        }

        // Get target position for hidden item
        Vector3 targetPos = positions[visiblePosIndex];

        // Phase 1: Move hidden item from current hidden position to opposite hidden position
        var seq = DOTween.Sequence();
        seq.Append(hiddenItem.transform.DOMove(hiddenIntermediatePos, 0));

        // Setup data for hidden item (use data of item that's going to hide)
        var dataIndex = GetDataIndexForPosition(visiblePosIndex);
        if (dataIndex >= 0 && dataIndex < currentDatas.Count)
        {
            hiddenItem.SetupData(currentDatas[dataIndex], ItemCount-1, visiblePosIndex, dataIndex);
        }

        // Activate hidden item
        hiddenItem.gameObject.SetActive(true);
        var canvasGroup = hiddenItem.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = hiddenItem.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;

        // Phase 2: Move hidden item from intermediate position to visible position
        var newScale = GetScaleFactor(visiblePosIndex);
        var newAlpha = GetFadeAlpha(visiblePosIndex);

        seq.Append(hiddenItem.transform.DOMove(targetPos, duration * 0.7f));
        seq.Join(hiddenItem.transform.DOScale(newScale, duration * 0.7f));
        seq.Join(canvasGroup.DOFade(newAlpha, duration * 0.7f));

        await seq.Play().AsyncWaitForCompletion();
    }

    private void FinalizeMoveAndSwap(int itemIndexToHide, int direction)
    {
        if (hiddenItem == null || ItemCount == 0)
        {
            return;
        }

        // Determine which item will become the new hidden item
        if (itemIndexToHide < 0 || itemIndexToHide >= ItemCount)
        {
            return;
        }

        // Swap: items[itemToHideIndex] becomes new hidden item, hidden item goes into visible array
        var temp = items[itemIndexToHide];
        items[itemIndexToHide] = hiddenItem;
        items[itemIndexToHide].transform.SetParent(content);
        hiddenItem = temp;
        temp.SetupData(null, -1, -1, -1); // Clear data for hidden item

        // Deactivate and position the new hidden item
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.transform.SetParent(hiddenItemRoot);
        hiddenItem.transform.position = (direction == -1) ? position_hidden_left.position : position_hidden_right.position;
    }

    private int GetDataIndexForPosition(int posIndex)
    {
        var gapIndex = posIndex - MiddlePosIndex;
        var dataIndex = (currentDataIndex + gapIndex) % DataCount;
        if (dataIndex < 0)
        {
            dataIndex += DataCount;
        }
        return dataIndex;
    }

    #endregion Task - Move!!!

    

    #region Task - Move Visual:

    /// <summary>
    /// After MappingPosition
    /// Play animation move items to new position
    /// direction: 1 for left to right, -1 for right to left
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="direction"></param>
    /// <param name="itemIndexToHide"></param>
    private async UniTask Move(float duration, int direction = 1, int itemIndexToHide = -1)
    {
        switch (direction)
        {
            case 1:
                await MoveToRight(duration, itemIndexToHide);
                break;
            case -1:
                await MoveToLeft(duration, itemIndexToHide);
                break;
        }
    }

    private async UniTask MoveToRight(float duration, int itemIndexToHide)
    {
        List<UniTask> moveTasks = new List<UniTask>();

        //Move items to right
        for (int i = items.Count-1; i >= 0; i--)
        {
            // Move item: {i} to position: {posIndex}
            int posIndex = itemToPosIndex[i];
            var targetPos = GetTargetPosition(posIndex);
            moveTasks.Add(MoveItem(i, targetPos, duration));
        }
        moveTasks.Add(MoveHiddenItemToRight(duration, itemIndexToHide));
        await UniTask.WhenAll(moveTasks);
    }

    private int FindItemByPosIndex(int posIndex)
    {
        foreach (var kvp in itemToPosIndex)
        {
            if (kvp.Value == posIndex)
            {
                return kvp.Key; // Return item index
            }
        }
        return -1; // Not found
    }

    private async UniTask MoveToLeft(float duration, int itemIndexToHide)
    {
        List<UniTask> moveTasks = new List<UniTask>();

        for (int i = 0; i < items.Count; i++)
        {
            // Move item: {i} to position: {posIndex}
            int posIndex = itemToPosIndex[i];
            var targetPos = GetTargetPosition(posIndex);
            moveTasks.Add(MoveItem(i, targetPos, duration));
        }

        moveTasks.Add(MoveHiddenItemToLeft(duration, itemIndexToHide));
        await UniTask.WhenAll(moveTasks);
    }

    private async UniTask MoveHiddenItemToLeft(float duration, int itemIndexToHide)
    {
        //Move hidden item to pos hidden left (instantly)
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.gameObject.transform.position = position_hidden_left.position;
        
        //Move Left => item shifts to right => item at LastPosIndex will be hidden
        Debug.Log($"MoveHiddenItemToLeft: Move ItemIndexToHide: {itemIndexToHide}");
        
        var targetPos = positions[FirstPosIndex];
        await _MoveHiddenItem(itemIndexToHide, targetPos, duration);
    }

    private async UniTask MoveHiddenItemToRight(float duration, int itemIndexToHide)
    {

        //Move hidden item to pos hidden right (instantly)
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.gameObject.transform.position = position_hidden_right.position;

        //Move Right => item shifts to left => item at FirstPosIndex will be hidden
        Debug.Log($"MoveHiddenItemToRight: Move ItemIndexToHide: {itemIndexToHide}");
        
        var targetPos = positions[LastPosIndex];
        await _MoveHiddenItem(itemIndexToHide, targetPos, duration);
    }

    private async UniTask _MoveHiddenItem(int itemIndex, Vector3 targetPos, float duration)
    {
        if (hiddenItem == null || ItemCount == 0 || currentDatas.Count == 0 || DataCount == 0)
        {
            return;
        }

        //SetupData:
        // Setup data for hidden item (use data of item that's going to hide)
        var posIndex = itemToPosIndex[itemIndex];
        var dataIndex = GetDataIndexForPosition(posIndex);
        if (dataIndex >= 0 && dataIndex < currentDatas.Count)
        {
            hiddenItem.SetupData(currentDatas[dataIndex], itemIndex, posIndex, dataIndex);
        }
        hiddenItem.gameObject.SetActive(true);

        // Get target position for hidden item
        // Vector3 targetPos = GetTargetPosition(posIndex);
        await _MoveItem(hiddenItem, itemIndex, targetPos, duration);
    }

    private Vector3 GetTargetPosition(int posIndex)
    {
        if (posIndex == POS_HIDDEN_LEFT)
        {
            return position_hidden_left.position;
        }
        else if (posIndex == POS_HIDDEN_RIGHT)
        {
            return position_hidden_right.position;
        }
        else
        {
            return positions[posIndex];
        }
    }

    private async UniTask MoveItem(int itemIndex, Vector3 targetPos, float duration)
    {
        var item = items[itemIndex];
        await _MoveItem(item, itemIndex, targetPos, duration);
    }

    private async System.Threading.Tasks.Task _MoveItem(CarouselRecycleItem item, int itemIndex, Vector3 targetPos, float duration)
    {
        // item.OnBeforeSelected(itemIndex == CurrentIndex);

        //move
        var seq = DOTween.Sequence();
        seq.Append(item.transform.DOMove(targetPos, duration));

        //scale:
        // var newPosIndex = itemToPosIndex[itemIndex];
        // var newScale = GetScaleFactor(newPosIndex);
        // seq.Join(item.transform.DOScale(newScale, duration));

        // //fade:
        // var newAlpha = GetFadeAlpha(newPosIndex);
        // var canvasGroup = item.GetComponent<CanvasGroup>();
        // if (!item.gameObject.activeSelf)
        // {
        //     item.gameObject.SetActive(true);
        // }

        // if (canvasGroup == null)
        // {
        //     canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
        // }
        // if (canvasGroup != null)
        // {
        //     seq.Join(canvasGroup.DOFade(newAlpha, duration));
        // }

        await seq.Play().AsyncWaitForCompletion();

        // item.OnSelected(itemIndex == CurrentIndex);
    }

    #region Task - Scale Item:

    private float GetScaleFactor(int newPosIndex)
    {
        var gap = Mathf.Abs(newPosIndex - MiddlePosIndex);
        switch (gap)
        {
            case 0:
                return 1f;
            default:
                return 0.7f;
        }
    }

    #endregion Task - Scale Item!!!

    #region Task - Fade Item:
    private float GetFadeAlpha(int newPosIndex)
    {
        var gap = Mathf.Abs(newPosIndex - MiddlePosIndex);
        switch (gap)
        {
            case 0:
            case 1:
                return 1f;
            default:
                return 0.3f;
        }
    }
    #endregion Task - Fade Item!!!
    #endregion Move Visual!!!

    #region Task - SetupItem:
    private void SetupItems()
    {
        if (isSetupComplete)
        {
            return;
        }
        
        ClearAllItems();
        GenerateItem();
        
        isSetupComplete = true;
    }

    private void ClearAllItems()
    {
        var chilldren = content.GetComponentsInChildren<Transform>();
        foreach (var child in chilldren)
        {
            if (child != content)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (var item in items)
        {
            Destroy(item.gameObject);
        }
        items.Clear();
    }

    private void GenerateItem<T>(List<T> datas) where T : class
    {
        isDuringSetup = true;
        for(var i=0; i < datas.Count; i++)
        {
            var item = Instantiate(itemPrefab, content);
            item.gameObject.name = $"Item{i}";
            // item.SetupData(datas[i]);
            items.Add(item);
        }
        isDuringSetup = false;
    }

    private void GenerateItem()
    {
        isDuringSetup = true;

        for (int i = 0; i < PositionCount; i++)
        {
            var item = Instantiate(itemPrefab, content);
            var position = positions[i];
            item.transform.position = position;
            item.gameObject.name = $"Item{i}";
            items.Add(item);
        }

        _GenerateHiddenItem();

        isDuringSetup = false;
    }

    private void _GenerateHiddenItem()
    {
        hiddenItem = Instantiate(itemPrefab, hiddenItemRoot);
        hiddenItem.transform.position = position_hidden_left.position;
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.gameObject.name = "HiddenItem";
    }

    public bool IsSetupComplete()
    {
        return isSetupComplete;
    }

    public UniTask WaitForReady()
    {
        return UniTask.WaitUntil(() => IsSetupComplete() && !isDuringSetup);
    }
    #endregion Task - SetupItems!!!


    #region Task - SetupPosition:
    private void SetupPosition()
    {
        positions = new List<Vector3>();
        for (int i = 0; i < positionRoot.childCount; i++)
        {
            positions.Add(positionRoot.GetChild(i).position);
        }
    }
    #endregion Task - SetupPosition!!!
}