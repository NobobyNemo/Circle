#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <iostream>

#pragma comment(lib, "ole32.lib")

int main() {
    std::cout << "=== Native C++ WASAPI Capture Test ===" << std::endl;

    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    std::cout << "CoInitialize: 0x" << std::hex << hr << std::endl;

    IMMDeviceEnumerator* enumerator = NULL;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), (void**)&enumerator);
    std::cout << "CreateEnumerator: 0x" << hr << std::endl;
    if (FAILED(hr)) return 1;

    IMMDevice* device = NULL;
    hr = enumerator->GetDefaultAudioEndpoint(eCapture, eConsole, &device);
    std::cout << "GetDefaultEndpoint: 0x" << hr << std::endl;
    if (FAILED(hr)) { enumerator->Release(); return 1; }

    LPWSTR id = NULL;
    device->GetId(&id);
    std::wcout << L"Device ID: " << id << std::endl;
    CoTaskMemFree(id);

    IAudioClient* audioClient = NULL;
    hr = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, NULL, (void**)&audioClient);
    std::cout << "Activate: 0x" << hr << std::endl;
    if (FAILED(hr)) { device->Release(); enumerator->Release(); return 1; }

    WAVEFORMATEX* mixFormat = NULL;
    hr = audioClient->GetMixFormat(&mixFormat);
    std::cout << "GetMixFormat: 0x" << hr << std::endl;
    if (SUCCEEDED(hr)) {
        std::cout << "Format: " << mixFormat->nSamplesPerSec << "Hz "
                  << mixFormat->wBitsPerSample << "bit " << mixFormat->nChannels << "ch" << std::endl;
    }

    hr = audioClient->Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 10000000, 0, mixFormat, NULL);
    std::cout << "Initialize: 0x" << std::hex << hr << std::endl;

    if (SUCCEEDED(hr)) {
        std::cout << "SUCCESS! WASAPI Initialize worked from native C++!" << std::endl;

        IAudioCaptureClient* captureClient = NULL;
        hr = audioClient->GetService(__uuidof(IAudioCaptureClient), (void**)&captureClient);
        std::cout << "GetCaptureClient: 0x" << hr << std::endl;

        if (SUCCEEDED(hr)) {
            hr = audioClient->Start();
            std::cout << "Start: 0x" << hr << std::endl;

            std::cout << "Reading for 3 seconds..." << std::endl;
            int packetCount = 0;
            for (int i = 0; i < 30; i++) {
                Sleep(100);
                UINT32 packetLength;
                while (SUCCEEDED(captureClient->GetNextPacketSize(&packetLength)) && packetLength > 0) {
                    BYTE* data;
                    UINT32 numFrames;
                    DWORD flags;
                    hr = captureClient->GetBuffer(&data, &numFrames, &flags, NULL, NULL);
                    if (SUCCEEDED(hr)) {
                        packetCount++;
                        if (packetCount <= 5)
                            std::cout << "  Packet: " << numFrames << " frames" << std::endl;
                        captureClient->ReleaseBuffer(numFrames);
                    }
                }
            }
            audioClient->Stop();
            std::cout << "Total packets: " << packetCount << std::endl;
            captureClient->Release();
        }
    } else {
        std::cout << "FAILED: WASAPI Initialize blocked (Kaspersky?)" << std::endl;
    }

    if (mixFormat) CoTaskMemFree(mixFormat);
    audioClient->Release();
    device->Release();
    enumerator->Release();
    CoUninitialize();

    std::cout << "=== Done ===" << std::endl;
    return 0;
}
