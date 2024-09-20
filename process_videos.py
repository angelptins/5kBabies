# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

import enum
import os
import sys
import argparse
import time
import utils
import cv2 as cv
import pandas as pd
import mediapipe as mp
import matplotlib.pyplot as plt
from pathlib import Path
from multiprocessing import Pool

# parse args and set defaults
parser = argparse.ArgumentParser(description='process video: extract tracked video, tracking coordinates, tracked trace image')

parser.add_argument('-id', '--input-directory',
                    help=('input directory for unannotated mp4 files; '
                          'if not specified default is ./raw_videos/'),
                    metavar='',
                    )

parser.add_argument('-nc', '--num-cores',
                    help=('(int >= 1) number of cores for parallel processing; '
                          'default is multiprocessing.cpu_count() - 2 (parallel processing), '
                          'entries over CPU count will '
                          'use multiprocessing.cpu_count() - 1 cores'
                          ),
                    type=int,
                    metavar='',
                    )

parser.add_argument('-fr', '--frame-resize',
                    help=('(float > 0.0) factor by which to resize video frames; '
                          'default is 0.8'
                          ),
                    type=float,
                    metavar=''
                    )

args = parser.parse_args()

if not args.input_directory:
    input_dir = Path('./raw_videos')
else:
    input_dir = Path(args.input_dir)
    if not os.path.isdir(input_dir):
        raise NotADirectoryError('input directory is not a valid director')
in_files = os.listdir(input_dir)

if args.frame_resize:
    frame_resize = args.frame_resize
    if frame_resize <= 0.0:
        raise ValueError('arg frame-resize must be float > 0')
else:
    frame_resize = 0.8

detection_confidence = 0.5
tracking_confidence = 0.5
vis_conf = 0.0
model_complexity = 2
num_cores = utils.set_num_cores(args.num_cores)

tracked_csv_dir = Path('./tracked_csvs')
tracked_images_dir = Path('./tracked_images')
tracked_videos_dir = Path('./tracked_videos')

# process files in input directory only if
names = [
    x.split('.')[0] for x in in_files
    if not os.path.isfile(os.path.join(tracked_images_dir, (x.split('.')[0] + '.png')))
]
print(names)
num_files = len(names)

# mediapipe info
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
pose = mp_pose.Pose(
    model_complexity=model_complexity,
    min_detection_confidence=detection_confidence,
    min_tracking_confidence=tracking_confidence
)

keypoint_names = [
    'nose',
    'left_eye_inner',
    'left_eye',
    'left_eye_outer',
    'right_eye_inner',
    'right_eye',
    'right_eye_outer',
    'left_ear',
    'right_ear',
    'mouth_left',
    'mouth_right',
    'left_shoulder',
    'right_shoulder',
    'left_elbow',
    'right_elbow',
    'left_wrist',
    'right_wrist',
    'left_pinky',
    'right_pinky',
    'left_index',
    'right_index',
    'left_thumb',
    'right_thumb',
    'left_hip',
    'right_hip',
    'left_knee',
    'right_knee',
    'left_ankle',
    'right_ankle',
    'left_heel',
    'right_heel',
    'left_foot_index',
    'right_foot_index',
]

# parameters for creating/cropping images
x_min = 0.15
x_max = 1 - x_min
y_min = 0.0
y_max = 1 - y_min
dpi = 100
image_dim = 500
fig_size = image_dim/dpi
alpha = 0.25

keypoint_colors = {
    'right_wrist': [27,158,119],
    'left_wrist': [231,41,138],
    'right_ankle': [217,95,2],
    'left_ankle': [117,112,179]
}
for key in keypoint_colors.keys():
    keypoint_colors[key] = [val/255. for val in keypoint_colors[key]]


# main function for processing videos
def main(ind, name):
    print(f'Processing file {ind + 1}/{num_files}')

    infile = os.path.join(input_dir, (name + '.mp4'))
    vid_outfile = os.path.join(tracked_videos_dir, (name + '_tracked.mp4'))
    csv_outfile = os.path.join(tracked_csv_dir, (name + '.csv'))
    img_outfile = os.path.join(tracked_images_dir, (name + '.png'))

    writer = None

    out_csv = [
        ['file_name','frame','landmark','x','y','z','visibility',]
    ]

    frame = 0

    cap = cv.VideoCapture(infile)
    fps = cap.get(cv.CAP_PROP_FPS)

    print('FPS: {}'.format(fps))

    with mp_pose.Pose(
        static_image_mode=False,
        min_detection_confidence=detection_confidence,
        min_tracking_confidence=tracking_confidence,
        model_complexity=model_complexity,
    ) as pose:
        while cap.isOpened():
            # get the video frame and mediapipe results
            ret, image = cap.read()
            if not ret:
                break
            
            image.flags.writeable = False
            image = cv.cvtColor(image, cv.COLOR_BGR2RGB)
            results = pose.process(image)

            # log csv info
            try:
                for i, point in enumerate(results.pose_landmarks.landmark):
                    out_csv.append(
                        [name, frame, keypoint_names[i], point.x, point.y, point.z, point.visibility,]
                    )

            except AttributeError:
                for point in keypoint_names:
                    out_csv.append(
                        [name, frame, point, 'NaN', 'NaN', 'NaN', 'NaN',]
                    )

            # annotate tracking on mp4 file
            image.flags.writeable = True
            image = cv.cvtColor(image, cv.COLOR_RGB2BGR)
            mp_drawing.draw_landmarks(
                image,
                results.pose_landmarks,
                mp_pose.POSE_CONNECTIONS,
                visibility_threshold = vis_conf
            )

            f_h = int(image.shape[1]*float(frame_resize))
            f_w = int(image.shape[0]*float(frame_resize))
            image = cv.resize(image, (f_h, f_w))

            if writer is None:
                W, H = image.shape[1], image.shape[0]
                fourcc = cv.VideoWriter_fourcc(*"mp4v")
                writer = cv.VideoWriter(vid_outfile, fourcc, fps, (W, H), True)
            writer.write(image)

            frame += 1

    cap.release()

    utils.save_list_csv(out_csv, csv_outfile)

    # plot trace image
    header = out_csv.pop(0)
    df = pd.DataFrame(out_csv, columns=header)

    fig, ax = plt.subplots(figsize=(fig_size, fig_size))
    fig.set_facecolor('black')

    for keypoint, color in keypoint_colors.items():
        data = df.loc[df.landmark == keypoint, ['x', 'y']]
        # for i in range(frames_per_image-1):
        for i in range(df.frame.max() - 2):
            x1, y1 = data.iloc[i]
            x2, y2 = data.iloc[i + 1]
            ax.plot(
                [x1, x2], [1-y1, 1-y2],
                color=color,
                marker=None,
                linestyle='-',
                alpha=alpha,
                linewidth=0.75
            )
    ax.set_axis_off()
    ax.set_xlim(x_min, x_max)
    ax.set_ylim(y_min, y_max)
    plt.tight_layout()

    plt.savefig(img_outfile, dpi=dpi)
    plt.close()


if __name__ == '__main__':
    if num_cores > 1:
        print('Running using {} cores.'.format(num_cores))
        pool = Pool(processes=num_cores)
        dummy = [
            pool.apply_async(main, (i, name)) for i, name in enumerate(names)
        ]
        pool.close()
        pool.join()
    else:
        for i, name in enumerate(names):
            main(i, name)

    print("\n\nDONE\n\n")


