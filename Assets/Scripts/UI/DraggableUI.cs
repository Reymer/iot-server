using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableUI : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform rectTransform;
    public Canvas canvas;
    private Vector2 originalLocalPointerPosition;
    private Vector2 originalPanelLocalPosition;

    public RectTransform dragArea;

    private Vector2 offset;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out originalLocalPointerPosition
        );

        originalPanelLocalPosition = rectTransform.localPosition;
        offset = originalPanelLocalPosition - originalLocalPointerPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragArea, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPosition))
        {
            Vector2 newPos = localPointerPosition + offset;

            Vector3[] dragAreaCorners = new Vector3[4];
            dragArea.GetWorldCorners(dragAreaCorners);

            Vector3[] uiCorners = new Vector3[4];
            rectTransform.GetWorldCorners(uiCorners);

            float minX = dragAreaCorners[0].x - uiCorners[0].x + rectTransform.localPosition.x;
            float maxX = dragAreaCorners[2].x - uiCorners[2].x + rectTransform.localPosition.x;
            float minY = dragAreaCorners[0].y - uiCorners[0].y + rectTransform.localPosition.y;
            float maxY = dragAreaCorners[2].y - uiCorners[2].y + rectTransform.localPosition.y;

            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.y = Mathf.Clamp(newPos.y, minY, maxY);

            rectTransform.localPosition = newPos;
        }
    }
}
