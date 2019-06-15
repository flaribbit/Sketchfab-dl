import sys
import argparse
 
if '--' in sys.argv:
    argv = sys.argv[sys.argv.index('--') + 1:]
    parser = argparse.ArgumentParser()
    parser.add_argument('-s1', '--sample_1', dest='sample_1', metavar='FILE')
    parser.add_argument('-s2', '--sample_2', dest='sample_2', type=int, required=False)
    args = parser.parse_known_args(argv)[0]
    # print parameters
    print('sample_1: ', args.sample_1)
    print('sample_2: ', args.sample_2)