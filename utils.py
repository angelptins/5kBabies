# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

import multiprocessing
import os
import json
import pickle
import csv
import sys
from pathlib import Path
from multiprocessing import cpu_count

import numpy as np

def set_dev_root():
    '''Create relative Path object for the root of this repo folder'''
    abspath = os.path.abspath(__file__)
    return Path(os.path.dirname(abspath))


def load_json(filepath):
    with open(filepath, 'r') as f:
        return json.load(f)


def save_json(object, filepath):
    with open(filepath, 'w') as f:
        json.dump(object, f)


def load_pickle(filepath):
    with open(filepath, 'rb') as f:
        return pickle.load(f)


def save_pickle(object, filepath):
    with open(filepath, 'wb') as f:
        pickle.dump(object, f)


def save_list_csv(object, filepath):
    '''Save a list of lists as a csv'''
    with open(filepath, 'w', newline='') as f:
        writer = csv.writer(f)
        writer.writerows(object)


def check_range(val, arg_name, min=0.0, max=1.0):
    '''
    check whether val is between min and max;
    if not return ValueError with arg_name
    '''
    if (float(val) < min) or (float(val) > max):
        raise ValueError(f'{arg_name} must be in range {min}-{max}')


def assign_float(val, arg_name):
    '''
    coerece an input number into a float;
    raises exception if input is not coercible
    '''
    try:
        return float(val)
    except Exception as e:
        print(e)
        sys.exit(0)


def assign_int(val, arg_name):
    '''
    coerce input number to integer;
    raises exception if input is not coercible
    '''
    try:
        return int(val)
    except Exception as e:
        print(e)
        sys.exit(0)


def set_model_complexity(complexity_arg):
    '''
    assign model complexity value for mediapipe data processing;
    if zero-type passed, default complexity is 2 (high complexity)
    '''
    if complexity_arg == 0:
        model_complexity = 0
    elif not complexity_arg:
        model_complexity = 2
    else:
        model_complexity = complexity_arg
        if (model_complexity < 0) or (model_complexity > 2):
            raise ValueError('model complexity must be one of 0, 1, or 2')

    return model_complexity


def set_num_cores(num_cores_arg):
    '''
    set number of processing cores for parallel processing;
    if zero-type is passed 1 core will be used (no parallel processing);
    if
    '''
    if num_cores_arg == 0:
        num_cores = 0
    elif not num_cores_arg:
        num_cores = multiprocessing.cpu_count() - 2
    else:
        num_cores = num_cores_arg
        if num_cores > cpu_count():
            print(f'Max system CPU count is {cpu_count()}; using {cpu_count() - 1} cores')
            num_cores = cpu_count() - 1
    if num_cores < 1:
        raise ValueError('num cores must be >= 1')

    return num_cores


def zero_one_arg(arg, arg_name, default=0.5):
    '''
    check if an argument passed falls between 0 and 1;
    assigns default value of 0.5 if none passed
    '''
    if arg == 0:
        val = 0
    elif arg:
        val = assign_float(arg, arg_name)
        check_range(val, arg_name)
    else:
        val = default

    return val


def float_range_arg(arg, arg_name, min, max, default):
    '''
    check range for float argument argument
    '''
    if arg == 0:
        val = 0
    elif arg:
        val = assign_float(arg, arg_name)
        if (val < min) or (val > max):
            raise ValueError(
                f'{arg_name} must be in range {float(min)}-{float(max)}'
                )
    else:
        val = default

    return float(val)


def int_range_arg(arg, arg_name, default, min=1, max=None):
    '''
    check range for integer argument
    '''
    if arg == 0:
        val = 0
    elif arg:
            val = arg

    else:
        val = default
    if min and (val < min):
        raise ValueError(f'{arg_name} must be >= {min}')
    elif max and (val > max):
        raise ValueError(f'{arg_name} must be <= {max}')

    return val


def print_elapsed_time(elapsed_time):
    '''
    helper function for formatting elapsed time console printing
    '''
    print(f'elapsed time: {elapsed_time:.3f} seconds')