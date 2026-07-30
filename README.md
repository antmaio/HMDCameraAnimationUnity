# Self-Avatar Full-body Tracking from HMD and Multiview Camera Rig in Unity

Unity application for self-avatar AI-based full-body tracking from HMd and a multi-camera rig setup.

<!-- ![Snapshot](docs/Snapshot_20260616_111553.png) -->

## Requirements 
- **Hardware:** 
- - Tested on MetaQuest 3
- - Nvidia GeForce RTX3080Ti
- - Works with Link cable 
- - Set up of calibrated RGB cameras
- - Calibration performed between HMD and external cameras    
- **Unity:** Tested on Unity 6000.1.14f1
- **Packages:**
    - Meta MR Utility Kit 201.0.0
    - Meta XR Core SDK 201.0.0
    - Inference Engine 2.2.2
    - OpenXR Plugin 1.15.1
    - XR Interaction Toolkit 3.1.3
    - XR Plugin Managment 4.5.3

## Installation
1. Create a Unity project
2. Install the required packages listed below.
3. Copy paste SMPL-X folder ```(./Assets/SMPLX)``` from [SMPL-X repo](https://gitlab.tuebingen.mpg.de/jtesch/smplx-unity) into ```./Assets/``` from your Unity project.
4. Clone the repository  
```bash 
     git clone https://github.com/antmaio/HMDCameraAnimationUnity
```
5. Download model weights from [Google Drive](https://drive.google.com/file/d/1QAA57ZB748kJsqwg2Go_xSLu_h8N_msV/view?usp=sharing) and paste them into ```./Assets/Neural Nets/```.

## Project Structure
After install, Unity project structure is supposed to look like:
```
Assets/
├── FullBodyAnimation
    ├── Scripts
        ├──
    ├── Neural Nets
        └── hmd-poser-ext-smplx_vr_and_3d-p1.onnx
    └── FullBodyAnimation.unity
├── SMPLX
├── MetaXR/
├── Oculus/          
├── Plugins/
├── Resouces/
├── Samples/
├── StreamingAssets/
├── XR/
└── XRI/
Packages/
```

## Usage
This application works on Play Mode and has not been tested on windows build yet.

## Features


## Usage



## Citation


<!-- 
## Configuration
 
Calibration parameters can be adjusted in `Assets/Resources/CalibrationConfig.json`:
 
```json
{
  "numCaptures": 20,
  "targetType": "checkerboard",
  "cameraCount": 4,
  "exportFormat": "json"
}
```


## Roadmap

 
- [ ] Support for additional HMD SDKs
- [ ] Automatic outlier rejection during calibration
- [ ] Real-time calibration accuracy feedback
## Contributing
 
Contributions are welcome. Please open an issue to discuss major changes before submitting a pull request.
 
## License
 
This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
 
## Acknowledgments
 
- List any papers, libraries, or prior work you're building on here.
 -->

