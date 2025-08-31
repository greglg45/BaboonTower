using UnityEngine;
using UnityEngine.EventSystems;

namespace BaboonTower.Game
{
    /// <summary>
    /// Contrôleur de caméra pour la GameScene
    /// Permet le zoom avec la molette et le déplacement de la caméra
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float smoothZoomSpeed = 5f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;
        [SerializeField] private bool useSmoothZoom = true;
        
        [Header("Pan Settings")]
        [SerializeField] private bool enablePanning = true;
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float panBorderThickness = 10f;
        [SerializeField] private bool enableEdgePanning = true;
        [SerializeField] private bool enableMiddleMousePanning = true;
        [SerializeField] private bool enableRightMousePanning = false;
        [SerializeField] private float dragSensitivity = 1f;
        
        [Header("Keyboard Controls")]
        [SerializeField] private bool enableKeyboardPan = true;
        [SerializeField] private KeyCode panUpKey = KeyCode.W;
        [SerializeField] private KeyCode panDownKey = KeyCode.S;
        [SerializeField] private KeyCode panLeftKey = KeyCode.A;
        [SerializeField] private KeyCode panRightKey = KeyCode.D;
        
        [Header("Map Boundaries")]
        [SerializeField] private bool constrainToMap = true;
        [SerializeField] private Vector2 mapSize = new Vector2(30, 16); // Taille de la map selon le GDD
        [SerializeField] private float tileSize = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        // Références
        private Camera cam;
        private MapLoaderV3 mapLoader;
        private MapGenerator mapGenerator;
        
        // État interne
        private float targetZoom;
        private Vector3 dragOrigin;
        private Vector3 lastMousePosition;
        private bool isDragging = false;
        private Vector2 mapBoundsMin;
        private Vector2 mapBoundsMax;
        
        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[CameraController] No camera found!");
                    enabled = false;
                    return;
                }
            }
            
            // S'assurer que la caméra est en mode orthographique
            cam.orthographic = true;
            targetZoom = cam.orthographicSize;
        }
        
        private void Start()
        {
            // Chercher les composants de map pour obtenir les limites
            mapLoader = FindObjectOfType<MapLoaderV3>();
            mapGenerator = FindObjectOfType<MapGenerator>();
            
            UpdateMapBounds();
            CenterCameraOnMap();
        }
        
        private void Update()
        {
            // Ne pas traiter les inputs si on est sur l'UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            
            HandleZoomInput();
            HandlePanInput();
            
            // Appliquer le zoom smooth si activé
            if (useSmoothZoom && Mathf.Abs(cam.orthographicSize - targetZoom) > 0.01f)
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, smoothZoomSpeed * Time.deltaTime);
            }
            
            // Contraindre la position de la caméra aux limites de la map
            if (constrainToMap)
            {
                ConstrainCameraPosition();
            }
        }
        
        /// <summary>
        /// Gère le zoom avec la molette de la souris
        /// </summary>
        private void HandleZoomInput()
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            
            if (scrollInput != 0)
            {
                // Calculer le nouveau zoom
                float zoomDelta = scrollInput * zoomSpeed;
                
                if (useSmoothZoom)
                {
                    targetZoom -= zoomDelta;
                    targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                }
                else
                {
                    cam.orthographicSize -= zoomDelta;
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[CameraController] Zoom: {cam.orthographicSize:F2} (Target: {targetZoom:F2})");
                }
            }
        }
        
        /// <summary>
        /// Gère le déplacement de la caméra
        /// </summary>
        private void HandlePanInput()
        {
            if (!enablePanning) return;
            
            Vector3 movement = Vector3.zero;
            
            // Déplacement avec le clavier
            if (enableKeyboardPan && !isDragging)
            {
                if (Input.GetKey(panUpKey)) movement.y += 1f;
                if (Input.GetKey(panDownKey)) movement.y -= 1f;
                if (Input.GetKey(panLeftKey)) movement.x -= 1f;
                if (Input.GetKey(panRightKey)) movement.x += 1f;
            }
            
            // Déplacement avec les bords de l'écran
            if (enableEdgePanning && !isDragging)
            {
                Vector3 mousePos = Input.mousePosition;
                
                if (mousePos.x <= panBorderThickness) movement.x -= 1f;
                if (mousePos.x >= Screen.width - panBorderThickness) movement.x += 1f;
                if (mousePos.y <= panBorderThickness) movement.y -= 1f;
                if (mousePos.y >= Screen.height - panBorderThickness) movement.y += 1f;
            }
            
            // Déplacement avec le bouton du milieu de la souris
            if (enableMiddleMousePanning)
            {
                HandleMouseDrag(2); // 2 = bouton du milieu
            }
            
            // Déplacement avec le clic droit (alternative)
            if (enableRightMousePanning)
            {
                HandleMouseDrag(1); // 1 = clic droit
            }
            
            // Appliquer le mouvement clavier/bords
            if (movement != Vector3.zero && !isDragging)
            {
                movement.Normalize();
                movement *= panSpeed * Time.deltaTime;
                
                // Ajuster la vitesse en fonction du zoom
                float zoomFactor = cam.orthographicSize / 10f;
                movement *= zoomFactor;
                
                transform.position += movement;
            }
        }
        
        /// <summary>
        /// Gère le drag de la souris pour déplacer la caméra
        /// </summary>
        private void HandleMouseDrag(int mouseButton)
        {
            // Début du drag
            if (Input.GetMouseButtonDown(mouseButton))
            {
                isDragging = true;
                dragOrigin = Input.mousePosition;
                lastMousePosition = Input.mousePosition;
                
                if (showDebugInfo)
                {
                    string buttonName = mouseButton == 2 ? "Middle" : "Right";
                    Debug.Log($"[CameraController] {buttonName} mouse drag started at: {dragOrigin}");
                }
            }
            
            // Pendant le drag
            if (Input.GetMouseButton(mouseButton) && isDragging)
            {
                Vector3 currentMousePosition = Input.mousePosition;
                Vector3 mouseDelta = lastMousePosition - currentMousePosition;
                
                // Convertir le delta de la souris en mouvement dans le monde
                float moveX = mouseDelta.x * dragSensitivity * cam.orthographicSize / Screen.height;
                float moveY = mouseDelta.y * dragSensitivity * cam.orthographicSize / Screen.height;
                
                Vector3 newPosition = transform.position;
                newPosition.x += moveX;
                newPosition.y += moveY;
                transform.position = newPosition;
                
                lastMousePosition = currentMousePosition;
                
                if (showDebugInfo && mouseDelta.magnitude > 0.1f)
                {
                    Debug.Log($"[CameraController] Dragging - Delta: {mouseDelta}, New pos: {transform.position}");
                }
            }
            
            // Fin du drag
            if (Input.GetMouseButtonUp(mouseButton) && isDragging)
            {
                isDragging = false;
                
                if (showDebugInfo)
                {
                    string buttonName = mouseButton == 2 ? "Middle" : "Right";
                    Debug.Log($"[CameraController] {buttonName} mouse drag ended");
                }
            }
        }
        
        /// <summary>
        /// Met à jour les limites de la map
        /// </summary>
        private void UpdateMapBounds()
        {
            // Essayer d'obtenir la taille de la map depuis les différents composants
            if (mapLoader != null && mapLoader.MapGrid != null)
            {
                mapSize.x = mapLoader.MapGrid.GetLength(0);
                mapSize.y = mapLoader.MapGrid.GetLength(1);
            }
            
            // Calculer les limites
            mapBoundsMin = Vector2.zero;
            mapBoundsMax = new Vector2(mapSize.x * tileSize, mapSize.y * tileSize);
        }
        
        /// <summary>
        /// Centre la caméra sur la map
        /// </summary>
        private void CenterCameraOnMap()
        {
            Vector3 centerPosition = new Vector3(
                mapSize.x * tileSize / 2f,
                mapSize.y * tileSize / 2f,
                transform.position.z
            );
            
            transform.position = centerPosition;
            
            // Ajuster le zoom initial pour voir toute la map
            float requiredZoomX = (mapSize.x * tileSize) / (2f * cam.aspect);
            float requiredZoomY = (mapSize.y * tileSize) / 2f;
            float initialZoom = Mathf.Max(requiredZoomX, requiredZoomY) * 1.1f; // +10% de marge
            
            cam.orthographicSize = Mathf.Clamp(initialZoom, minZoom, maxZoom);
            targetZoom = cam.orthographicSize;
        }
        
        /// <summary>
        /// Contraint la position de la caméra aux limites de la map
        /// </summary>
        private void ConstrainCameraPosition()
        {
            float height = cam.orthographicSize;
            float width = height * cam.aspect;
            
            float minX = mapBoundsMin.x + width;
            float maxX = mapBoundsMax.x - width;
            float minY = mapBoundsMin.y + height;
            float maxY = mapBoundsMax.y - height;
            
            // Si la caméra voit plus que la map, centrer
            if (minX > maxX)
            {
                minX = maxX = (mapBoundsMin.x + mapBoundsMax.x) / 2f;
            }
            if (minY > maxY)
            {
                minY = maxY = (mapBoundsMin.y + mapBoundsMax.y) / 2f;
            }
            
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            transform.position = pos;
        }
        
        /// <summary>
        /// Définit le niveau de zoom (public API)
        /// </summary>
        public void SetZoom(float zoomLevel)
        {
            zoomLevel = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
            
            if (useSmoothZoom)
            {
                targetZoom = zoomLevel;
            }
            else
            {
                cam.orthographicSize = zoomLevel;
            }
        }
        
        /// <summary>
        /// Centre la caméra sur une position donnée
        /// </summary>
        public void FocusOnPosition(Vector3 worldPosition)
        {
            worldPosition.z = transform.position.z;
            transform.position = worldPosition;
            ConstrainCameraPosition();
        }
        
        /// <summary>
        /// Centre la caméra sur une position de grille
        /// </summary>
        public void FocusOnGridPosition(Vector2Int gridPos)
        {
            Vector3 worldPos = new Vector3(
                gridPos.x * tileSize + tileSize * 0.5f,
                gridPos.y * tileSize + tileSize * 0.5f,
                transform.position.z
            );
            FocusOnPosition(worldPos);
        }
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (!constrainToMap) return;
            
            // Dessiner les limites de la map
            Gizmos.color = Color.yellow;
            Vector3 bottomLeft = new Vector3(mapBoundsMin.x, mapBoundsMin.y, 0);
            Vector3 bottomRight = new Vector3(mapBoundsMax.x, mapBoundsMin.y, 0);
            Vector3 topLeft = new Vector3(mapBoundsMin.x, mapBoundsMax.y, 0);
            Vector3 topRight = new Vector3(mapBoundsMax.x, mapBoundsMax.y, 0);
            
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
            
            // Dessiner la zone visible de la caméra
            if (cam != null)
            {
                Gizmos.color = Color.cyan;
                float height = cam.orthographicSize * 2;
                float width = height * cam.aspect;
                Gizmos.DrawWireCube(transform.position, new Vector3(width, height, 0));
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUI.Box(new Rect(10, 10, 250, 180), "Camera Controller Debug");
            GUI.Label(new Rect(20, 30, 220, 20), $"Zoom: {cam.orthographicSize:F2}");
            GUI.Label(new Rect(20, 50, 220, 20), $"Target: {targetZoom:F2}");
            GUI.Label(new Rect(20, 70, 220, 20), $"Position: {transform.position.x:F1}, {transform.position.y:F1}");
            GUI.Label(new Rect(20, 90, 220, 20), $"Map: {mapSize.x}x{mapSize.y}");
            GUI.Label(new Rect(20, 110, 220, 20), isDragging ? "DRAGGING" : "Ready");
            GUI.Label(new Rect(20, 130, 220, 20), $"Mouse Pos: {Input.mousePosition.x:F0}, {Input.mousePosition.y:F0}");
            
            // Test des boutons de souris
            string mouseButtons = "";
            if (Input.GetMouseButton(0)) mouseButtons += "Left ";
            if (Input.GetMouseButton(1)) mouseButtons += "Right ";
            if (Input.GetMouseButton(2)) mouseButtons += "Middle ";
            GUI.Label(new Rect(20, 150, 220, 20), $"Mouse Buttons: {(string.IsNullOrEmpty(mouseButtons) ? "None" : mouseButtons)}");
        }
        
        #endregion
    }
}