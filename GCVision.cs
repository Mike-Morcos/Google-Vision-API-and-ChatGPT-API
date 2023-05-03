using FrostweepGames.Plugins.Core;
using OpenAI;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;
using System.IO;


namespace FrostweepGames.Plugins.GoogleCloud.Vision
{
    public class GCVision : MonoBehaviour
    {
        public static bool callChat;
        public event Action<VisionImageResponse, long> AnnotateImagesSuccessEvent;
        public event Action<VisionFileResponse, long> AnnotateFilesSuccessEvent;
       
        public event Action<string, long> AnnotateImagesFailedEvent;
        public event Action<string, long> AnnotateFilesFailedEvent;

        [SerializeField] ChatGPTResponse chatGPTResponse;



        private static GCVision _Instance;
        public static GCVision Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new GameObject("[Singleton]GCVision").AddComponent<GCVision>();

                return _Instance;
            }
        }

        private IVisionManager _visionManager;

        public ServiceLocator ServiceLocator { get { return ServiceLocator.Instance; } }

        [Header("Prefab Object Settings")]
        public bool isDontDestroyOnLoad = false;
        public bool isFullDebugLogIfError = false;
        public bool isUseAPIKeyFromPrefab = false;

        [Header("Prefab Fields")]
        [PasswordField]
        // get api key using a public field

        private void Awake()
        {
            if (_Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            if (isDontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _Instance = this;


            ServiceLocator.Register<IVisionManager>(new VisionManager());
            ServiceLocator.Register<IFileManager>(new FileManager());

            ServiceLocator.InitServices();

            _visionManager = ServiceLocator.Get<IVisionManager>();

            _visionManager.AnnotateImagesSuccessEvent += AnnotateImagesSuccessEventHandler;
            _visionManager.AnnotateFilesSuccessEvent += AnnotateFilesSuccessEventHandler;
            _visionManager.AnnotateImagesFailedEvent += AnnotateImagesFailedEventHandler;
            _visionManager.AnnotateFilesFailedEvent += AnnotateFilesFailedEventHandler;

  

        }

        private void Update()
        {
            if (_Instance == this)
            {
                ServiceLocator.Instance.Update();
            }
        }

        private void OnDestroy()
        {
            if (_Instance == this)
            {
                _visionManager.AnnotateImagesSuccessEvent -= AnnotateImagesSuccessEventHandler;
                _visionManager.AnnotateFilesSuccessEvent -= AnnotateFilesSuccessEventHandler;
                _visionManager.AnnotateImagesFailedEvent -= AnnotateImagesFailedEventHandler;
                _visionManager.AnnotateFilesFailedEvent -= AnnotateFilesFailedEventHandler;

                _Instance = null;
                ServiceLocator.Instance.Dispose();
            }
        }

        /// <summary>
        /// Request for performing Google Cloud Vision API tasks over a user-provided image, with user-requested features, and with context information.
        /// </summary>
        /// <param name="requests"></param>
        public void AnnotateImages(List<AnnotateImageRequest> requests)
        {
            //--------------------------------------------------------------------------------------------------
            foreach (var request in requests)
            {
                request.features = new List<Feature>();

                // Add TEXT_DETECTION feature to detect regular text
                request.features.Add(new Feature { type = Enumerators.FeatureType.TEXT_DETECTION });

                // Add DOCUMENT_TEXT_DETECTION feature to detect handwritten text
                request.features.Add(new Feature { type = Enumerators.FeatureType.DOCUMENT_TEXT_DETECTION });
            }

            _visionManager.AnnotateImages(requests);

            //----------------------------------------------------------------------------------------------------
            //--- _visionManager.AnnotateImages(requests);
        }

        /// <summary>
        /// A request to annotate files, e.g. a PDF, TIFF or GIF files
        /// </summary>
        /// <param name="requests"></param>
        public void AnnotateFiles(List<AnnotateFileRequest> requests)
        {
            _visionManager.AnnotateFiles(requests);
        }

        private void AnnotateImagesSuccessEventHandler(VisionImageResponse arg1, long arg2)
        {
            if (AnnotateImagesSuccessEvent != null)
                AnnotateImagesSuccessEvent(arg1, arg2);

            // Reset the text annotation to an empty string
            ChatGPTResponse.annotatedResponseText = "";

            // Check if the response contains text annotations
            if (arg1.responses != null && arg1.responses.Length > 0 && arg1.responses[0].textAnnotations != null && arg1.responses[0].textAnnotations.Length > 0)
            {
                // Get the text from the first text annotation
                string textAnnotation = arg1.responses[0].textAnnotations[0].description;

                ChatGPTResponse.annotatedResponseText = textAnnotation;

                //chatGptResponse.SetActive(true);
                StartCoroutine(chatGPTResponse.SubmitChat());
            }
        }



        private void AnnotateImagesFailedEventHandler(string arg1, long arg2)
        {
            if (AnnotateImagesFailedEvent != null)
                AnnotateImagesFailedEvent(arg1, arg2);
        }

        private void AnnotateFilesSuccessEventHandler(VisionFileResponse arg1, long arg2)
        {
            if (AnnotateFilesSuccessEvent != null)
                AnnotateFilesSuccessEvent(arg1, arg2);
        }

        private void AnnotateFilesFailedEventHandler(string arg1, long arg2)
        {
            if (AnnotateFilesFailedEvent != null)
                AnnotateFilesFailedEvent(arg1, arg2);
        }

        public void uploadPicture()
        {
            NativeFilePicker.Permission permission = NativeFilePicker.PickFile((path) =>
            {
                if (path != null)
                {
                    byte[] imageData = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageData);

                    // Check image dimensions
                    if (texture.width < 640 || texture.height < 480)
                    {
                        Debug.Log("Image dimensions are too small");
                        return;
                    }

                    // Check image file size
                    if (imageData.Length > 4 * 1024 * 1024)
                    {
                        Debug.Log("Image file size is too large");
                        return;
                    }

                    AnnotateImageRequest request = new AnnotateImageRequest()
                    {
                        image = new Image()
                        {
                            content = Convert.ToBase64String(imageData)
                        },
                        features = new List<Feature>()
                {
                    new Feature()
                    {
                        type = Enumerators.FeatureType.TEXT_DETECTION
                    },
                    new Feature()
                    {
                        type = Enumerators.FeatureType.DOCUMENT_TEXT_DETECTION
                    }
                }
                    };
                    AnnotateImages(new List<AnnotateImageRequest>() { request });
                }
            }, "Select an image", "image/*");

            Debug.Log("Permission result: " + permission);
        }
        public void TakePicture()
        {
            NativeCamera.Permission permission = NativeCamera.TakePicture((path) =>
            {
                if (path != null)
                {
                    Texture2D texture = NativeCamera.LoadImageAtPath(path, 640, false, true);
                    if (texture == null)
                    {
                        Debug.LogError("Failed to load image from path: " + path);
                        return;
                    }

                    byte[] imageData = texture.EncodeToJPG(50); // Reduce image quality to compress image
                    int maxSize = 4 * 1024 * 1024; // 4 MB
                    if (imageData.Length > maxSize)
                    {
                        Debug.LogError("Image size exceeds 4 MB limit");
                        return;
                    }

                    AnnotateImageRequest request = new AnnotateImageRequest()
                    {
                        image = new Image()
                        {
                            content = Convert.ToBase64String(imageData)
                        },
                        features = new List<Feature>()
                {
                    new Feature()
                    {
                        type = Enumerators.FeatureType.TEXT_DETECTION
                    },
                    new Feature()
                    {
                        type = Enumerators.FeatureType.DOCUMENT_TEXT_DETECTION
                    }
                }
                    };
                    AnnotateImages(new List<AnnotateImageRequest>() { request });
                }
            }, 512);

            Debug.Log("Permission result: " + permission);
        }


    }






}