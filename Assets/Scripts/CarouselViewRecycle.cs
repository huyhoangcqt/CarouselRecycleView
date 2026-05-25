using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private bool isMoving = false;
    private int currentDataIndex = 0; //NOTE: currentIndex is the index of item prefab, and also the index of data, because we have same count of item prefab and data, and 1 by 1 mapping
    public int DataCount = 0;
    private Dictionary<int, int> itemToDataIndex = new Dictionary<int, int>(); //mapping item index to data index
    private Dictionary<Transform, Tween> activeItemTweens = new Dictionary<Transform, Tween>();
    private CancellationTokenSource moveCancellationSource;
    private int currentMoveItemIndexToHide = -1;
    private int currentMoveDirection = 0;


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
        InitMappingData();

        UpdateDebugItemDataList();
        // Setup initial visual state (scale + alpha) for all items immediately after mapping is ready
        SetupInitialVisuals();
    }

    private void InitMappingData()
    {
        CurrentIndex = 0;
        currentDataIndex = 0;

        for (int i = 0; i < ItemCount; i++)
        {
            itemToPosIndex[i] = i;
        }

        RefreshItemDataIndexMapping();
    }

    public void SetupDatas<T>(List<T> datas, int startIndex) where T : class
    {    
        currentDataIndex = startIndex;
        DataCount = datas.Count;
        currentDatas.Clear();
        currentDatas.AddRange(datas.Cast<object>());
        RefreshItemDataIndexMapping();

        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var dataIndex = itemToDataIndex[itemIndex];
            var item = items[itemIndex];
            item.SetupData(datas[dataIndex], itemIndex, itemToPosIndex[itemIndex], dataIndex);
        }

        UpdateDebugItemDataList();
    }

    #endregion Main!!!

    #region Task - Calculate Data Mapping:

    private void RefreshItemDataIndexMapping()
    {
        if (DataCount <= 0)
        {
            return;
        }

        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            itemToDataIndex[itemIndex] = GetDataIndexForPosition(itemToPosIndex[itemIndex]);
        }
    }


    private Vector3 GetHiddenSlotPosition(int direction)
    {
        return direction == -1 ? position_hidden_right.position : position_hidden_left.position;
    }

    private int GetDataIndexForPosition(int posIndex)
    {
        return WrapIndex(currentDataIndex + (posIndex - MiddlePosIndex), DataCount);
    }

    private int WrapIndex(int index, int size)
    {
        if (size <= 0)
        {
            return 0;
        }

        var wrapped = index % size;
        return wrapped < 0 ? wrapped + size : wrapped;
    }


    private void ReAsignItemDataIndex()
    {
        for (var itemIndex = 0; itemIndex < ItemCount; itemIndex++)
        {
            var posIndex = itemToPosIndex[itemIndex];
            var dataIndex = GetDataIndexForPosition(posIndex);
            itemToDataIndex[itemIndex] = dataIndex;
            items[itemIndex].SetupData(currentDatas[dataIndex], itemIndex, posIndex, dataIndex);
        }
    }

    private void ShiftItemPositions(int direction)
    {
        foreach (var itemIndex in itemToPosIndex.Keys.ToList())
        {
            itemToPosIndex[itemIndex] = WrapPositionIndex(itemToPosIndex[itemIndex] - direction);
        }
    }

    private int GetVisibleSlotIndex(int direction)
    {
        return direction == 1 ? LastPosIndex : FirstPosIndex;
    }

    private int GetHiddenSlotIndex(int direction)
    {
        return direction == 1 ? POS_HIDDEN_LEFT : POS_HIDDEN_RIGHT;
    }

    private int WrapPositionIndex(int posIndex)
    {
        if (posIndex < POS_HIDDEN_LEFT)
        {
            posIndex += PositionCount;
        }
        else if (posIndex > POS_HIDDEN_RIGHT)
        {
            posIndex -= PositionCount;
        }
        return posIndex;
    }

    private bool IsValidIndex(int index) => index >= 0 && index < ItemCount;

    #endregion Task - Calculate Data Mapping!!!

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
        if (isMoving)
        {
            Debug.Log("MoveLeft: interrupting current move.");
            InterruptCurrentMove();
        }

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
        if (isMoving)
        {
            Debug.Log("MoveRight: interrupting current move.");
            InterruptCurrentMove();
        }

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
        var thisMoveCancellationSource = RegisterNewMove();
        var cancellationToken = thisMoveCancellationSource.Token;

        Debug.Log("MoveToIndex: " + index + ", direction: " + direction);
        if (!IsValidIndex(index))
        {
            Debug.LogError($"Index: {index} is out of range");
            return;
        }

        if (isMoving)
        {
            Debug.Log("MoveToIndex: starting new move while previous move was interrupted.");
        }

        isMoving = true;
        try
        {
            currentMoveItemIndexToHide = FindItemToHide(direction);
            currentMoveDirection = direction;

            await WaitForReady();
            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log("Select index: " + index);

            CurrentIndex = index;
            ShiftItemPositions(direction);
            RefreshItemDataIndexMapping();
            Debug.Log($"Move to index: {index}, direction: {direction}");
            
            // Phase 1 & 2: Move all items + Move hidden item in parallel
            await UniTask.WhenAll(
                Move(duration, direction, currentMoveItemIndexToHide)
            ).AttachExternalCancellation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            // Phase 3: Finalize swap
            // Finalize swap first to update item/hidden references and mappings,
            // then reassign data to visible items based on updated mappings.
            FinalizeMoveAndSwap(currentMoveItemIndexToHide, direction);
            ReAsignItemDataIndex();
            UpdateDebugItemDataList();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("MoveToIndex cancelled.");
        }
        finally
        {
            if (moveCancellationSource == thisMoveCancellationSource)
            {
                isMoving = false;
            }
        }
    }

    private void FinalizeMoveAndSwap(int itemIndexToHide, int direction)
    {
        if (hiddenItem == null || ItemCount == 0 || !IsValidIndex(itemIndexToHide))
        {
            return;
        }

        // 1) Tính vị trí visible mà hidden item sẽ chiếm sau khi swap.
        var visiblePosIndex = GetVisibleSlotIndex(direction);

        // 2) Swap object: item tại itemIndexToHide trở thành hiddenItem mới,
        //    và hiddenItem hiện tại chuyển vào vị trí visible đó.
        var temp = items[itemIndexToHide];
        items[itemIndexToHide] = hiddenItem;
        items[itemIndexToHide].transform.SetParent(content);

        // 3) Cập nhật mapping vị trí và data cho item mới visible.
        itemToPosIndex[itemIndexToHide] = visiblePosIndex;
        itemToDataIndex[itemIndexToHide] = GetDataIndexForPosition(visiblePosIndex);

        // 4) Thiết lập lại hiddenItem thành item bị swap ra ngoài.
        hiddenItem = temp;
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.transform.SetParent(hiddenItemRoot);
        hiddenItem.transform.position = GetHiddenSlotPosition(direction);

        // 5) Cập nhật data cho hidden item mới dựa trên vị trí hidden.
        var hiddenPosIndex = GetHiddenSlotIndex(direction);
        var hiddenDataIndex = GetDataIndexForPosition(hiddenPosIndex);
        if (hiddenDataIndex >= 0 && hiddenDataIndex < currentDatas.Count)
        {
            hiddenItem.SetupData(currentDatas[hiddenDataIndex], itemIndexToHide, hiddenPosIndex, hiddenDataIndex);
        }
        else
        {
            Debug.LogWarning($"Data index: {hiddenDataIndex} is out of range for currentDatas count: {currentDatas.Count}. Setting hidden item data to null.");
            hiddenItem.SetupData(null, itemIndexToHide, hiddenPosIndex, -1);
        }
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
        await MoveHiddenItemToSlot(duration, itemIndexToHide, position_hidden_left.position, FirstPosIndex, "MoveHiddenItemToLeft");
    }

    private async UniTask MoveHiddenItemToRight(float duration, int itemIndexToHide)
    {
        await MoveHiddenItemToSlot(duration, itemIndexToHide, position_hidden_right.position, LastPosIndex, "MoveHiddenItemToRight");
    }

    private async UniTask MoveHiddenItemToSlot(float duration, int itemIndexToHide, Vector3 startPos, int destinationPosIndex, string debugLabel)
    {
        hiddenItem.gameObject.SetActive(false);
        hiddenItem.transform.position = startPos;

        Debug.Log($"{debugLabel}: Move ItemIndexToHide: {itemIndexToHide}");

        var targetPos = GetTargetPosition(destinationPosIndex);
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

        // Move hidden item using unified move helper
        await MoveItemInternal(hiddenItem, itemIndex, targetPos, duration);
    }

    private void KillActiveMoveTween(Transform itemTransform)
    {
        if (itemTransform == null)
        {
            return;
        }

        if (activeItemTweens.TryGetValue(itemTransform, out var existingTween) && existingTween.IsActive())
        {
            existingTween.Kill();
        }

        activeItemTweens.Remove(itemTransform);
    }

    private void RegisterActiveMoveTween(Transform itemTransform, Tween tween)
    {
        if (itemTransform == null || tween == null)
        {
            return;
        }

        KillActiveMoveTween(itemTransform);
        activeItemTweens[itemTransform] = tween;
        tween.OnKill(() => activeItemTweens.Remove(itemTransform));
    }

    private void KillAllActiveMoveTweens()
    {
        foreach (var tween in activeItemTweens.Values.ToList())
        {
            if (tween.IsActive())
            {
                tween.Kill();
            }
        }

        activeItemTweens.Clear();
    }

    private CancellationTokenSource RegisterNewMove()
    {
        if (moveCancellationSource != null)
        {
            moveCancellationSource.Cancel();
        }

        moveCancellationSource = new CancellationTokenSource();
        return moveCancellationSource;
    }

    private void CancelCurrentMove()
    {
        if (moveCancellationSource == null)
        {
            return;
        }

        if (!moveCancellationSource.IsCancellationRequested)
        {
            moveCancellationSource.Cancel();
        }
    }

    private void InterruptCurrentMove()
    {
        if (!isMoving)
        {
            return;
        }

        Debug.Log("Interrupting current move and starting new command.");
        CancelCurrentMove();
        KillAllActiveMoveTweens();
        ForceCompleteCurrentMove();
        isMoving = false;
    }

    private void ForceCompleteCurrentMove()
    {
        if (currentMoveItemIndexToHide < 0 || currentMoveDirection == 0)
        {
            return;
        }

        SnapItemsToCurrentMapping();
        var itemIndexToHide = currentMoveItemIndexToHide;
        var direction = currentMoveDirection;
        currentMoveItemIndexToHide = -1;
        currentMoveDirection = 0;

        FinalizeMoveAndSwap(itemIndexToHide, direction);
        ReAsignItemDataIndex();
        UpdateDebugItemDataList();
    }

    private void SnapItemsToCurrentMapping()
    {
        foreach (var kvp in itemToPosIndex)
        {
            var itemIndex = kvp.Key;
            var posIndex = kvp.Value;
            if (itemIndex >= 0 && itemIndex < items.Count)
            {
                var item = items[itemIndex];
                item.transform.position = GetTargetPosition(posIndex);
                item.transform.localScale = Vector3.one * GetScaleFactor(posIndex);
                var cg = item.GetComponent<CanvasGroup>() ?? item.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = GetFadeAlpha(posIndex);
            }
        }

        if (hiddenItem != null && currentMoveDirection != 0)
        {
            var visiblePosIndex = GetVisibleSlotIndex(currentMoveDirection);
            hiddenItem.transform.position = GetTargetPosition(visiblePosIndex);
            hiddenItem.transform.localScale = Vector3.one * GetScaleFactor(visiblePosIndex);
            hiddenItem.gameObject.SetActive(true);
            var cg = hiddenItem.GetComponent<CanvasGroup>() ?? hiddenItem.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = GetFadeAlpha(visiblePosIndex);
        }
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
        await MoveItemInternal(item, itemIndex, targetPos, duration);
    }
    private async UniTask MoveItemInternal(CarouselRecycleItem item, int itemIndex, Vector3 targetPos, float duration)
    {
        if (item == null)
        {
            return;
        }

        item.OnBeforeSelected(itemIndex == CurrentIndex);
        KillActiveMoveTween(item.transform);

        //move
        var seq = DOTween.Sequence();
        seq.Append(item.transform.DOMove(targetPos, duration));

        //scale:
        var newPosIndex = itemToPosIndex.ContainsKey(itemIndex) ? itemToPosIndex[itemIndex] : FirstPosIndex;
        var newScale = GetScaleFactor(newPosIndex);
        seq.Join(item.transform.DOScale(newScale, duration));

        //fade:
        var newAlpha = GetFadeAlpha(newPosIndex);
        var canvasGroup = item.GetComponent<CanvasGroup>();
        if (!item.gameObject.activeSelf)
        {
            item.gameObject.SetActive(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
        }
        if (canvasGroup != null)
        {
            seq.Join(canvasGroup.DOFade(newAlpha, duration));
        }

        RegisterActiveMoveTween(item.transform, seq);
        await seq.Play().AsyncWaitForCompletion();

        item.OnSelected(itemIndex == CurrentIndex);
    }

    /// <summary>
    /// Initialize scale and alpha for items and hidden item immediately after setup/mapping.
    /// </summary>
    private void SetupInitialVisuals()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var posIndex = itemToPosIndex.ContainsKey(i) ? itemToPosIndex[i] : FirstPosIndex;
            var scale = GetScaleFactor(posIndex);
            item.transform.localScale = Vector3.one * scale;

            var cg = item.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = item.gameObject.AddComponent<CanvasGroup>();
            }
            cg.alpha = GetFadeAlpha(posIndex);
        }

        if (hiddenItem != null)
        {
            var hiddenScale = GetScaleFactor(POS_HIDDEN_LEFT);
            hiddenItem.transform.localScale = Vector3.one * hiddenScale;
            var cgH = hiddenItem.GetComponent<CanvasGroup>() ?? hiddenItem.gameObject.AddComponent<CanvasGroup>();
            cgH.alpha = GetFadeAlpha(POS_HIDDEN_LEFT);
        }
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

    #region Other: 

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

        Debug.Log($"Invalid direction: {direction}. Expected 1 for MoveRight or -1 for MoveLeft.");
        return -1; // Invalid direction
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

        Debug.LogError("FindItemByPosIndex: No item found at posIndex: " + posIndex);
        return -1; // Not found
    }

    #endregion Other!!!

    #region Debug:

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

    
    [System.Serializable]
    public class ItemData
    {
        public int Index;//item index
        public int PosIndex;//position index
        public int DataIndex;//data index
        public bool isHidden;
    }
    [SerializeField] private List<ItemData> debugItemDataList = new List<ItemData>();

    #endregion Debug!!!
}