using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.Android;
using Unity.WebRTC;
using System.Net.NetworkInformation;

namespace WebRTCTutorial.UI
{
    public class UIManager : MonoBehaviour
    {
#if UNITY_EDITOR
        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnValidate.html
        protected void OnValidate()
        {
            try
            {
                // Validate that all references are connected
                Assert.IsNotNull(_peerViewA);
                Assert.IsNotNull(_peerViewB);
                Assert.IsNotNull(_cameraDropdown);
                Assert.IsNotNull(_connectButton);
                Assert.IsNotNull(_disconnectButton);
            }
            catch (Exception)
            {
                Debug.LogError(
                    $"Some of the references are NULL, please inspect the {nameof(UIManager)} script on this object",
                    this);
            }
        }
#endif

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html
        protected void Awake()
        {
            Debug.Log($"UiManager Awake");
            // FindObjectOfType is used for the demo purpose only. In a real production it's better to avoid it for performance reasons
            _videoManager = FindObjectOfType<VideoManager>();

            // Android���� ī�޶� ���� ��û
            if (Application.platform == RuntimePlatform.Android)
            {
                RequestCameraPermission();
                RequestMicrophonePermission();
            }

            // Check if there's any camera device available
            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError(
                    "No Camera devices available! Please make sure a camera device is detected and accessible by Unity. " +
                    "This demo application will not work without a camera device.");
            }

            // Subscribe to buttons
            _connectButton.onClick.AddListener(OnConnectButtonClicked);
            _disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);

            // Clear default options from the dropdown
            _cameraDropdown.ClearOptions();

            // Populate dropdown with the available camera devices
            foreach (var cameraDevice in WebCamTexture.devices)
            {
                _cameraDropdown.options.Add(new TMP_Dropdown.OptionData(cameraDevice.name));
            }

            // Change the active camera device when new dropdown value is selected
            _cameraDropdown.onValueChanged.AddListener(SetActiveCamera);

            // Subscribe to when video from the other peer is received
            _videoManager.RemoteVideoReceived += OnRemoteVideoReceived;
            Debug.Log($"UiManager Awake");
        }

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        protected void Start()
        {
            // Enable first camera from the dropdown.
            // We call it in Start to make sure that Awake of all game objects completed and all scripts 
            SetActiveCamera(deviceIndex: 0);
        }

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html
        protected void Update()
        {
            // Control buttons being clickable by the connection state
            _connectButton.interactable = _videoManager.CanConnect;
            _disconnectButton.interactable = _videoManager.IsConnected;
        }

        [SerializeField]
        private PeerView _peerViewA;

        [SerializeField]
        private PeerView _peerViewB;

        [SerializeField]
        private TMP_Dropdown _cameraDropdown;

        [SerializeField]
        private Button _connectButton;

        public AudioSource _audioSource;


        [SerializeField]
        private Button _disconnectButton;

        private WebCamTexture _activeCamera;

        private AudioClip micClip;

        private VideoManager _videoManager;

        private RTCPeerConnection _peerConnection; // Add the peer connection variable

        // Android���� ī�޶� ������ ��û�ϴ� �޼���
        private void RequestCameraPermission()
        {
            Debug.Log("video");
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
            }
        }
        private void RequestMicrophonePermission()
        {
            Debug.Log("mircrophone");
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.Log("mircrophone");
                Permission.RequestUserPermission(Permission.Microphone);
            }
        }


        private void SetActiveCamera(int deviceIndex)
        {
            string deviceName = _cameraDropdown.options[deviceIndex].text;
            WebCamDevice[] devices = WebCamTexture.devices;

            // ���� ī�޶� �ڵ����� �����ϴ� ����
            string frontCamera = null;
            foreach (var device in devices)
            {
                if (device.isFrontFacing) // ���� ī�޶� Ȯ��
                {
                    frontCamera = device.name;
                    break;
                }
            }
            _activeCamera = new WebCamTexture(frontCamera, 1024, 768, requestedFPS: 30);
            _activeCamera.Play();
            // ī�޶� ���� Ȯ��
            if (!_activeCamera.isPlaying)
            {
                Debug.LogError($"Failed to start the {deviceName} camera device.");
                return;
            }

            // 1. ����ũ ����
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("No Microphone devices available!");
                return;
            }

            var microphoneDeviceName = Microphone.devices[0]; // ù ��° ����ũ ����
            micClip = Microphone.Start(microphoneDeviceName,true,600, 44100);

            if (Microphone.IsRecording(microphoneDeviceName))
            {
                Debug.Log("Microphone is recording.");
            }
            else
            {
                Debug.LogError("Microphone is not recording.");
            }
            Debug.Log("Microphone started successfully.");

            // 2. ī�޶�� ����ũ�� VideoManager�� ����
            StartCoroutine(PassActiveCameraAndMicToVideoManager(_activeCamera, micClip));
        }

        /// <summary>
        /// Starting the camera is an asynchronous operation.
        /// If we create the video track before camera is active it may have an invalid resolution.
        /// Therefore, it's best to wait until camera is in fact started before passing it to the video track
        /// </summary>
        private IEnumerator PassActiveCameraAndMicToVideoManager(WebCamTexture _activeCamera, AudioClip _activeMicrophone)
        {
            var timeElapsed = 0f;
            while (!_activeCamera.didUpdateThisFrame)
            {
                yield return null;

                // infinite loop prevention
                timeElapsed += Time.deltaTime;
                if (timeElapsed > 5f)
                {
                    Debug.LogError("Camera didn't start after 5 seconds. Aborting. The video track is not created.");
                    yield break;
                }
            }

            // Set preview of the local peer (Peer A) with the original camera texture
           
            _peerViewA.SetVideoTexture(_activeCamera);
            _peerViewA.audioSource.clip=  _activeMicrophone;
            _peerViewA.audioSource.Play();

            _videoManager.SetActiveCameraAndMicrophone(_activeCamera, _peerViewA.audioSource);

            if (_audioSource == null)
            {
                Debug.Log("no clip");
            }

            else
            {
                Debug.Log("yes clip");
            }
            // Set preview of the remote peer (Peer B) with the original camera texture
            // _peerViewB.SetVideoTexture( /* Remote Video Texture */ );

            // Rotate PeerView A and PeerView B GameObjects by 90 degrees on Y-axis
            _peerViewA.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees on Y-axis
            _peerViewB.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees on Y-axis
            Debug.Log("good");


            // Notify Video Manager about new active camera device and microphone
            
            
            //_audioSource.Play();
            // Check if peerConnection is null before proceeding
            if (_peerConnection == null)
            {
                Debug.LogWarning("Peer connection is null. Initializing peer connection now.");
                _peerConnection = new RTCPeerConnection(); // Initialize the peer connection here if null
            }
        }
       

        private void OnRemoteVideoReceived(Texture texture)
        {
            // OnRemoteVideoReceived���� ���� �ؽ�ó�� �״�� ����
            _peerViewB.SetVideoTexture(texture);

            // �Ǿ� B�� ���� ������Ʈ�� 90�� ȸ��
            _peerViewB.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees on Y-axis
        }
        private void OnRemoteAudioReceived(AudioClip audioClip)
        {
            // OnRemoteVideoReceived���� ���� �ؽ�ó�� �״�� ����
            _peerViewB.SetAudioSource(audioClip);

            // �Ǿ� B�� ���� ������Ʈ�� 90�� ȸ��

        }

        private void OnConnectButtonClicked()
        {
            // Ensure peerConnection is initialized
            if (_peerConnection == null)
            {
                Debug.LogWarning("Peer connection is null, initializing fuck");
                _peerConnection = new RTCPeerConnection(); // Ensure the peer connection is initialized
                Debug.LogWarning("go go!!");
            }

            // Now try to connect
            if (_videoManager.CanConnect)
            {
                Debug.LogWarning("connect");
                _videoManager.Connect();  // Assuming _videoManager handles the actual connection logic
            }
            else
            {
                Debug.LogWarning("Cannot connect, please check the peer connection status.");
            }
        }


        private void OnDisconnectButtonClicked()
        {
            _videoManager.Disconnect();
        }
    }
}
