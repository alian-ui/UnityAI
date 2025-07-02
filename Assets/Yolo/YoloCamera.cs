using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;


public class YoloCamera : MonoBehaviour
{
    // For YOLO
    //
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset modelAsset;
    Tensor<float> centersToCorners;
    //
    const BackendType backend = BackendType.GPUCompute;
    //
    private Transform displayLocation;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    //
    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;

    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;

    [Header("Camera Settings")]
    [Tooltip("Target width for the camera resolution")]
    public int targetWidth = 640;
    
    [Tooltip("Target height for the camera resolution")]
    public int targetHeight = 640;
    
    [Tooltip("Target frame rate for the camera")]
    public int targetFPS = 30;

    [Header("UI Components")]
    [Tooltip("Raw Image component to display the camera feed")]
    public RawImage displayImage;
    
    [Header("Save Settings")]
    [Tooltip("Prefix for saved image files")]
    public string imagePrefix = "WebCam_";
    
    [Tooltip("Image format for saving (PNG or JPG)")]
    public ImageFormat saveFormat = ImageFormat.PNG;

    public enum ImageFormat
    {
        PNG,
        JPG
    }

    // Private variables
    private WebCamTexture webCamTexture;
    private WebCamDevice[] availableCameras;
    private int currentCameraIndex = 0;
    private bool isInitialized = false;
    private string saveDirectory;

    void Start()
    {
        // Set up save directory
        saveDirectory = Path.Combine(Application.persistentDataPath, "CapturedImages");
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        // Initialize camera
        StartCoroutine(InitializeCamera());
        targetRT = new RenderTexture(targetWidth, targetHeight, 0);

        //Parse neural net labels
        labels = classesAsset.text.Split('\n');

        LoadModel();
    }
    void LoadModel()
    {
        //Load model
        var model1 = ModelLoader.Load(modelAsset);

        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
                    1,      0,      1,      0,
                    0,      1,      0,      1,
                    -0.5f,  0,      0.5f,   0,
                    0,      -0.5f,  0,      0.5f
        });

        //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model1);
        var modelOutput = Functional.Forward(model1, inputs)[0];                        //shape=(1,84,8400)
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);               //shape=(8400,4)
        var allScores = modelOutput[0, 4.., ..];                                //shape=(80,8400)
        var scores = Functional.ReduceMax(allScores, 0);                                //shape=(8400)
        var classIDs = Functional.ArgMax(allScores, 0);                                 //shape=(8400)
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));   //shape=(8400,4)
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold); //shape=(N)
        var coords = Functional.IndexSelect(boxCoords, 0, indices);                     //shape=(N,4)
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);                    //shape=(N)

        //Create worker to run model
        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    IEnumerator InitializeCamera()
    {
        // Request camera permission (important for mobile platforms)
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield break;
        }

        // Get available cameras
        availableCameras = WebCamTexture.devices;
        
        if (availableCameras.Length == 0)
        {
            yield break;
        }

        // Start the first available camera
        StartCamera(currentCameraIndex);
    }

    void StartCamera(int cameraIndex)
    {
        if (cameraIndex < 0 || cameraIndex >= availableCameras.Length)
        {
            return;
        }

        // Stop current camera if running
        StopCamera();

        // Create new WebCamTexture
        string deviceName = availableCameras[cameraIndex].name;
        webCamTexture = new WebCamTexture(deviceName, targetWidth, targetHeight, targetFPS);
        
        // Start the camera
        webCamTexture.Play();
        
        // Wait for camera to start
        StartCoroutine(WaitForCameraStart());
    }

    IEnumerator WaitForCameraStart()
    {        
        // Wait for camera to start (timeout after 10 seconds)
        float timeout = 10f;
        float timer = 0f;
        
        while (!webCamTexture.isPlaying && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (!webCamTexture.isPlaying)
        {
            yield break;
        }

        // Wait for first frame
        while (webCamTexture.width <= 16)
        {
            yield return null;
        }

        // Set up display
        if (displayImage != null)
        {
            displayImage.texture = webCamTexture;
            
            // Adjust aspect ratio
            AspectRatioFitter aspectRatio = displayImage.GetComponent<AspectRatioFitter>();
            if (aspectRatio == null)
            {
                aspectRatio = displayImage.gameObject.AddComponent<AspectRatioFitter>();
            }
            aspectRatio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectRatio.aspectRatio = (float)webCamTexture.width / webCamTexture.height;
        }

        isInitialized = true;
    }

    void StopCamera()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }
        
        isInitialized = false;
    }

    public void CaptureImage()
    {
        if (!isInitialized || webCamTexture == null || !webCamTexture.isPlaying)
        {
            return;
        }
        StartCoroutine(CaptureImageCoroutine());
    }

    public void ExecuteML()
    {
        if (!isInitialized || !webCamTexture)
        {
            return;
        }

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, targetHeight, targetWidth));
        TextureConverter.ToTensor(webCamTexture, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / targetWidth;
        float scaleY = displayHeight / targetHeight;

        int boxesFound = output.shape[0];
        //Draw the bounding boxes
        if (boxesFound == 0)
        {
            Debug.Log("Not found");
        }
        else
        {
            for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
            {
                var label = labels[labelIDs[n]];
                Debug.Log(label);
            }
        }
    }

    IEnumerator CaptureImageCoroutine()
    {
        // Create a Texture2D from the current camera frame
        Texture2D capturedImage = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        capturedImage.SetPixels(webCamTexture.GetPixels());
        capturedImage.Apply();

        // Generate filename with timestamp
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string extension = saveFormat == ImageFormat.PNG ? ".png" : ".jpg";
        string filename = imagePrefix + timestamp + extension;
        string filepath = Path.Combine(saveDirectory, filename);

        // Convert to bytes and save
        byte[] imageBytes;
        if (saveFormat == ImageFormat.PNG)
        {
            imageBytes = capturedImage.EncodeToPNG();
        }
        else
        {
            imageBytes = capturedImage.EncodeToJPG(90); // 90% quality
        }

        try
        {
            File.WriteAllBytes(filepath, imageBytes);
            Debug.Log($"Image captured and saved to: {filepath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save image: {e.Message}");
        }

        // Clean up
        Destroy(capturedImage);
        
        yield return null;
    }

    public void OnCameraChanged(int newCameraIndex)
    {
        if (newCameraIndex != currentCameraIndex && newCameraIndex < availableCameras.Length)
        {
            currentCameraIndex = newCameraIndex;
            StartCamera(currentCameraIndex);
        }
    }

    // Public methods for external control
    public bool IsInitialized => isInitialized;
    public WebCamTexture CurrentCamera => webCamTexture;
    public string SaveDirectory => saveDirectory;

    public Texture2D GetCurrentFrame()
    {
        if (!isInitialized || webCamTexture == null || !webCamTexture.isPlaying)
        {
            return null;
        }

        Texture2D currentFrame = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        currentFrame.SetPixels(webCamTexture.GetPixels());
        currentFrame.Apply();
        return currentFrame;
    }

    void Update()
    {
        // Handle keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CaptureImage();
            ExecuteML();
        }
                
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    void OnDestroy()
    {
        StopCamera();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopCamera();
        }
        else if (availableCameras != null && availableCameras.Length > 0)
        {
            StartCamera(currentCameraIndex);
        }
    }
}
