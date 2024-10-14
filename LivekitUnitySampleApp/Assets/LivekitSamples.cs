using System.Collections;
using UnityEngine;
using LiveKit;
using LiveKit.Proto;
using UnityEngine.UI;
using RoomOptions = LiveKit.RoomOptions;
using System.Collections.Generic;
using TMPro;

public class LivekitSamples : MonoBehaviour
{
    public string url = "ws://localhost:7880";
    public string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE3MTkyODQ5NzgsImlzcyI6IkFQSXJramtRYVZRSjVERSIsIm5hbWUiOiJ1bml0eSIsIm5iZiI6MTcxNzQ4NDk3OCwic3ViIjoidW5pdHkiLCJ2aWRlbyI6eyJjYW5VcGRhdGVPd25NZXRhZGF0YSI6dHJ1ZSwicm9vbSI6ImxpdmUiLCJyb29tSm9pbiI6dHJ1ZX19.WHt9VItlQj0qaKEB_EIxyFf2UwlqdEdWIiuA_tM0QmI";

    private Room room = null;

    private int frameRate = 30;

    Dictionary<string, GameObject> _videoObjects = new();
    List<RemoteAudioTrackHandler> audioTrackHandlers = new();
    Dictionary<string, RemoteAudioTrackHandler> audioTrackHandlersPerId = new();
    List<RtcVideoSource> _rtcVideoSources = new();
    List<VideoStream> _videoStreams = new();

    public GridLayoutGroup layoutGroup; //Component

    public TMP_Text statusText;
    public RemoteAudioTrackHandler remoteAudioTrackHandlerPrefab;
    public RemoteVideoTrackHandler remoteVideoTrackHandlerPrefab;

    public void UpdateStatusText(string newText)
    {
        if (statusText != null)
        {
            statusText.text = newText;
        }
    }

    public void OnClickPublishAudio()
    {
        StartCoroutine(publishMicrophone());
        Debug.Log("OnClickPublishAudio clicked!");
    }

    public void onClickPublishData()
    {
        publishData();
        Debug.Log("onClickPublishData clicked!");
    }

    public void onClickMakeCall()
    {
        Debug.Log("onClickMakeCall clicked!");
        StartCoroutine(MakeCall());
    }

    public void onClickHangup()
    {
        Debug.Log("onClickHangup clicked!");
        room.Disconnect();
        CleanUp();
        room = null;
        UpdateStatusText("Disconnected");
    }

    IEnumerator MakeCall()
    {
        if(room == null)
        {
            room = new Room();
            room.TrackSubscribed += TrackSubscribed;
            room.TrackUnsubscribed += UnTrackSubscribed;
            room.DataReceived += DataReceived;
            var options = new RoomOptions();
            var connect = room.Connect(url, token, options);
            yield return connect;
            if (!connect.IsError)
            {
                Debug.Log("Connected to " + room.Name);
                UpdateStatusText("Connected");
            }
        }
        
    }

    void TrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
    {
        if (track is RemoteVideoTrack videoTrack)
        {
            AddVideoTrack(videoTrack);
        }
        else if (track is RemoteAudioTrack audioTrack)
        {
            GameObject audObject = new GameObject(audioTrack.Sid);
            var source = audObject.AddComponent<AudioSource>();
            var stream = new AudioStream(audioTrack, source);
            audioTrackHandlersPerId.TryAdd(audioTrack.Sid, GetRemoteAudioTrackHandler());
            audioTrackHandlersPerId[audioTrack.Sid] = audObject;
        }
    }

    void AddVideoTrack(RemoteVideoTrack videoTrack)
    {
        Debug.Log("AddVideoTrack " + videoTrack.Sid);

        GameObject imgObject = new GameObject(videoTrack.Sid);

        RectTransform trans = imgObject.AddComponent<RectTransform>();
        trans.localScale = Vector3.one;
        trans.sizeDelta = new Vector2(180, 120);
        trans.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 180f, 50), Vector3.forward);

        RawImage image = imgObject.AddComponent<RawImage>();

        var stream = new VideoStream(videoTrack);
        stream.TextureReceived += (tex) =>
        {
            if (image != null)
            {
                image.texture = tex;
            }
        };

        _videoObjects[videoTrack.Sid] = imgObject;

        imgObject.transform.SetParent(layoutGroup.gameObject.transform, false);
        stream.Start();
        StartCoroutine(stream.Update());
        _videoStreams.Add(stream);
    }

    void UnTrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
    {
        if (track is RemoteVideoTrack videoTrack)
        {
            var imgObject = _videoObjects[videoTrack.Sid];
            if(imgObject != null)
            {
                Destroy(imgObject);
            }
            _videoObjects.Remove(videoTrack.Sid);
        }
        else if (track is RemoteAudioTrack audioTrack)
        {
            var audObject = audioTrackHandlersPerId[audioTrack.Sid];
            if (audObject != null)
            {
                var source = audObject.GetComponent<AudioSource>();
                source.Stop();
                Destroy(audObject);
            }
            audioTrackHandlersPerId.Remove(audioTrack.Sid);
        }
    }

    void DataReceived(byte[] data, Participant participant, DataPacketKind kind, string topic)
    {
        var str = System.Text.Encoding.Default.GetString(data);
        Debug.Log("DataReceived: from " + participant.Identity + ", data " + str);
        UpdateStatusText("DataReceived: from " + participant.Identity + ", data " + str);
    }

    public IEnumerator publishMicrophone()
    {
        Debug.Log("publicMicrophone!");
        // Publish Microphone
        var localSid = "my-audio-source";
        GameObject audObject = new GameObject(localSid);
        var source = audObject.AddComponent<AudioSource>();
        source.clip = Microphone.Start(Microphone.devices[0], true, 2, (int)RtcAudioSource.DefaultMirophoneSampleRate);
        source.loop = true;

        audioTrackHandlersPerId[localSid] = audObject;

        var rtcSource = new RtcAudioSource(source);
        Debug.Log("CreateAudioTrack!");
        var track = LocalAudioTrack.CreateAudioTrack("my-audio-track", rtcSource, room);

        var options = new TrackPublishOptions();
        options.AudioEncoding = new AudioEncoding();
        options.AudioEncoding.MaxBitrate = 64000;
        options.Source = TrackSource.SourceMicrophone;

        Debug.Log("PublishTrack!");
        var publish = room.LocalParticipant.PublishTrack(track, options);
        yield return publish;

        if (!publish.IsError)
        {
            Debug.Log("Track published!");
        }

        rtcSource.Start();
    }

    public void publishData()
    {
        var str = "hello from unity!";
        room.LocalParticipant.PublishData(System.Text.Encoding.Default.GetBytes(str));
    }

    void CleanUp()
    {
        foreach (var item in audioTrackHandlersPerId)
        {
            var source = item.Value.GetComponent<AudioSource>();
            source.Stop();
            Destroy(item.Value);
        }

        audioTrackHandlersPerId.Clear();

        foreach (var item in _videoObjects)
        {
            RawImage img = item.Value.GetComponent<RawImage>();
            if (img != null)
            {
                img.texture = null;
                Destroy(img);
            }

            Destroy(item.Value);
        }

        foreach (var item in _videoStreams)
        {
            item.Stop();
            item.Dispose();
        }

        foreach (var item in _rtcVideoSources)
        {
            item.Stop();
            item.Dispose();
        }

        _videoObjects.Clear();

        _videoStreams.Clear();
    }

    RemoteAudioTrackHandler GetRemoteAudioTrackHandler()
    {
        foreach (var item in audioTrackHandlers)
        {
            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
                return item;
            }
        }
        audioTrackHandlers.Add(Instantiate(remoteAudioTrackHandlerPrefab, transform));
        return audioTrackHandlers[audioTrackHandlers.Count - 1];
    }
}
