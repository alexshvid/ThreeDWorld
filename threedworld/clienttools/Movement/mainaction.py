import time
import zmq
import time
import os
import socket
#import multiprocessing
import sys
import numpy as np
#import h5py
import json
from PIL import Image
from StringIO import StringIO
from actions.curious import make_new_batch
from environment import config

#path = 'C:/Users/mrowca/Documents/test'
#path = '/home/mrowca/Desktop/images'
path = '/Users/damian/Desktop/test_images'

#file = h5py.File(path, mode='a')
#valid = file.require_dataset('valid', shape=(N,), dtype=np.bool)
#images = file.require_dataset('images', shape=(N, 256, 256, 3), dtype=np.uint8)
#normals = file.require_dataset('normals', shape=(N, 256, 256, 3), dtype=np.uint8)
#objects = file.require_dataset('objects', shape=(N, 256, 256, 3), dtype=np.uint8)

#TODO: rather hacky, but works for now  
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

ctx = zmq.Context()
def loop():
	print "connecting..."
	global sock 
	sock = ctx.socket(zmq.REQ)
	sock.connect("tcp://" + host_address + ":5556")
	print "... connected @" + host_address + ":" + "5556"

	print "sending join..."
	#sock.send_json({"msg_type" : "SWITCH_SCENES", "get_obj_data" : True, "send_scene_info" : True})
	#sock.send_json({"msg_type" : "CLIENT_JOIN", "get_obj_data" : True, "send_scene_info" : True})
        sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]})
	print "...join sent"

	bn = 0
	while True:
		print "waiting on messages"
		make_new_batch(bn, sock, path)
		print "messages received"
		bn = bn + 1
	
def check_port_num(port_num):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
		s.bind((host_address, int(port_num)))
    except socket.error as e:
		s.close()
		if (e.errno == 98):
			return False
		elif (e.errno == 48):
			return False
		else:
			raise e
    s.close()
    return True

def check_if_env_up():
	while True:
		time.sleep(5)
		if (check_port_num(5556)):
			sys.exit()

#t1 = multiprocessing.Process(target=loop)
#t2 = multiprocessing.Process(target=check_if_env_up)

#t1.start()
#t2.start()

#while True:
#	time.sleep(3)
#	if (not t2.is_alive()):
#		t1.terminate()
#		sys.exit()
#	elif (not t1.is_alive()):
#		t2.terminate()
#		sys.exit()

loop()