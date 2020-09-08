// DECODE.C - An LZW decoder for GIF
// Copyright (C) 1987, by Steven A. Bennett
//
// Permission is given by the author to freely redistribute and include
// this code in any program as long as this credit is given where due.
//
// In accordance with the above, I want to credit Steve Wilhite who wrote
// the code which this is heavily inspired by...
//
// GIF and 'Graphics Interchange Format' are trademarks (tm) of
// Compuserve, Incorporated, an H&R Block Company.
//
// Release Notes: This file contains a decoder routine for GIF images
// which is similar, structurally, to the original routine by Steve Wilhite.
// It is, however, somewhat noticably faster in most cases.

#region ReSharper
// ReSharper disable IdentifierTypo
#endregion

using System.Linq;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// This is a 1:1 port of VPinball's lzwreader which is used to decompress
	/// bitmaps.
	/// </summary>
	/// <see href="https://github.com/vpinball/vpinball/blob/master/media/lzwreader.cpp"/>
	public class LzwReader
	{
		private const int MaxCodes = 4095;
		private readonly int[] _codeMask = {
			0,
			0x0001, 0x0003,
			0x0007, 0x000F,
			0x001F, 0x003F,
			0x007F, 0x00FF,
			0x01FF, 0x03FF,
			0x07FF, 0x0FFF
		};

		private readonly BufferPtr _pstm;

		/* output */
		private readonly BufferPtr _pbBitsOutCur;
		private readonly int _cbStride;
		private int _badCodeCount;

		/* Static variables */
		private int _currSize;                 /* The current code size */
		private int _clear;                    /* Value for a clear code */
		private int _ending;                   /* Value for a ending code */
		private int _newCodes;                 /* First available code */
		private int _topSlot;                  /* Highest code for current size */
		private int _slot;                     /* Last read code */

		/* The following static variables are used
		 * for separating out codes
		 */
		private int _numAvailBytes;              /* # bytes left in block */
		private int _numBitsLeft;                /* # bits left in current byte */
		private byte _b1;                        /* Current byte */
		private readonly byte[] _byteBuff = new byte[257];      /* Current block */
		private BufferPtr _pBytes;                              /* points to byte_buff - Pointer to next byte in block */

		private readonly byte[] _stack = new byte[MaxCodes + 1];     /* Stack for storing pixels */
		private readonly byte[] _suffix = new byte[MaxCodes + 1];    /* Suffix table */
		private readonly int[] _prefix = new int[MaxCodes + 1];      /* Prefix linked list */

		private readonly int _width;
		private readonly int _height;
		private int _linesLeft;

		public LzwReader(byte[] pstm, int width, int height, int pitch) {
			for (var i = 0; i < MaxCodes + 1; i++) {
				_prefix[i] = 0;
			}
			_cbStride = pitch;
			_pbBitsOutCur = new BufferPtr(new byte[pitch * height]);

			_badCodeCount = 0;

			_pstm = new BufferPtr(pstm);

			_width = width; // 32-bit picture
			_height = height;
			_linesLeft = height + 1; // +1 because 1 gets taken o
		}

		public void Decompress(out byte[] data, out int length) {
			int fc;

			// Initialize for decoding a new image...
			const int size = 8;
			InitExp(size);

			// Initialize in case they forgot to put in a clear code.
			// (This shouldn't happen, but we'll try and decode it anyway...)
			var oc = fc = 0;

			// Allocate space for the decode buffer
			var buf = NextLine();

			// Set up the stack pointer and decode buffer pointer
			var sp = new BufferPtr(_stack);
			var bufPtr = BufferPtr.FromPtr(buf);
			var bufCnt = _width;

			// This is the main loop.  For each code we get we pass through the
			// linked list of prefix codes, pushing the corresponding "character" for
			// each code onto the stack.  When the list reaches a single "character"
			// we push that on the stack too, and then start unstacking each
			// character for output in the correct order.  Special handling is
			// included for the clear code, and the whole thing ends when we get
			// an ending code.
			var c = GetNextCode();
			while (c != _ending) {

				// If we had a file error, return without completing the decode
				if (c < 0) {
					break;
				}

				// If the code is a clear code, reinitialize all necessary items.
				if (c == _clear) {
					_currSize = size + 1;
					_slot = _newCodes;
					_topSlot = 1 << _currSize;

					// Continue reading codes until we get a non-clear code
					// (Another unlikely, but possible case...)
					c = GetNextCode();
					while (c == _clear) {
						c = GetNextCode();
					}

					// If we get an ending code immediately after a clear code
					// (Yet another unlikely case), then break out of the loop.
					if (c == _ending) {
						break;
					}

					// Finally, if the code is beyond the range of already set codes,
					// (This one had better NOT happen...  I have no idea what will
					// result from this, but I doubt it will look good...) then set it
					// to color zero.
					if (c >= _slot) {
						c = 0;
					}

					oc = fc = c;

					// And var us not forget to put the char into the buffer... And
					// if, on the off chance, we were exactly one pixel from the end
					// of the line, we have to send the buffer to the out_line()
					// routine...
					bufPtr.Set((byte)c);
					bufPtr.Incr();

					if (--bufCnt == 0) {
						buf = NextLine();
						bufPtr = BufferPtr.FromPtr(buf);
						bufCnt = _width;
					}

				} else {

					// In this case, it's not a clear code or an ending code, so
					// it must be a code code...  So we can now decode the code into
					// a stack of character codes. (Clear as mud, right?)
					var code = c;

					// Here we go again with one of those off chances...  If, on the
					// off chance, the code we got is beyond the range of those already
					// set up (Another thing which had better NOT happen...) we trick
					// the decoder into thinking it actually got the last code read.
					// (Hmmn... I'm not sure why this works...  But it does...)
					if (code >= _slot) {
						if (code > _slot) {
							++_badCodeCount;
						}
						code = oc;
						sp.Set((byte)fc);
						sp.Incr();
					}

					// Here we scan back along the linked list of prefixes, pushing
					// helpless characters (ie. suffixes) onto the stack as we do so.
					while (code >= _newCodes) {
						sp.Set(_suffix[code]);
						sp.Incr();
						code = _prefix[code];
					}

					// Push the last character on the stack, and set up the new
					// prefix and suffix, and if the required slot number is greater
					// than that allowed by the current bit size, increase the bit
					// size.  (NOTE - If we are all full, we *don't* save the new
					// suffix and prefix...  I'm not certain if this is correct...
					// it might be more proper to overwrite the last code...
					sp.Set((byte)code);
					sp.Incr();
					if (_slot < _topSlot) {
						fc = code;
						_suffix[_slot] = (byte)fc;	// = code;
						_prefix[_slot++] = oc;
						oc = c;
					}
					if (_slot >= _topSlot) {
						if (_currSize < 12) {
							_topSlot <<= 1;
							++_currSize;
						}
					}

					// Now that we've pushed the decoded string (in reverse order)
					// onto the stack, lets pop it off and put it into our decode
					// buffer...  And when the decode buffer is full, write another
					// line...
					while (sp.GetPos() > 0) {

						sp.Decr();
						bufPtr.Set(sp.Get());
						bufPtr.Incr();
						if (--bufCnt == 0) {
							buf = NextLine();
							bufPtr = buf;
							bufCnt = _width;
						}
					}
				}
				c = GetNextCode();
			}

			data = _pbBitsOutCur.GetBuffer();
			length = _pstm.GetPos();
		}

		private void InitExp(int size) {
			_currSize = size + 1;
			_topSlot = 1 << _currSize;
			_clear = 1 << size;
			_ending = _clear + 1;
			_slot = _newCodes = _ending + 1;
			_numAvailBytes = _numBitsLeft = 0;
		}

		private BufferPtr NextLine() {
			var pbRet = BufferPtr.FromPtr(_pbBitsOutCur);
			_pbBitsOutCur.Incr(_cbStride);	// fucking upside down dibs!
			_linesLeft--;
			return pbRet;
		}

		private int GetNextCode() {
			int ret;
			if (_numBitsLeft == 0) {
				if (_numAvailBytes <= 0) {

					// Out of bytes in current block, so read next block
					_pBytes = new BufferPtr(_byteBuff);
					_numAvailBytes = GetByte();
					if (_numAvailBytes < 0) {
						return _numAvailBytes;

					}

					if (_numAvailBytes > 0) {
						for (var i = 0; i < _numAvailBytes; ++i) {
							var x = GetByte();
							if (x < 0) {
								return x;
							}
							_byteBuff[i] = (byte)x;
						}
					}
				}
				_b1 = _pBytes.Get();
				_pBytes.Incr();
				_numBitsLeft = 8;
				--_numAvailBytes;
			}

			ret = _b1 >> (8 - _numBitsLeft);
			while (_currSize > _numBitsLeft) {
				if (_numAvailBytes <= 0) {

					// Out of bytes in current block, so read next block
					_pBytes = new BufferPtr(_byteBuff);
					_numAvailBytes = GetByte();
					if (_numAvailBytes < 0) {
						return _numAvailBytes;

					}

					if (_numAvailBytes > 0) {
						for (var i = 0; i < _numAvailBytes; ++i) {
							var x = GetByte();
							if (x < 0) {
								return x;
							}
							_byteBuff[i] = (byte)x;
						}
					}
				}
				_b1 = _pBytes.Get();
				_pBytes.Incr();
				ret |= _b1 << _numBitsLeft;
				_numBitsLeft += 8;
				--_numAvailBytes;
			}
			_numBitsLeft -= _currSize;
			ret &= _codeMask[_currSize];
			return ret;
		}

		private int GetByte() {
			return _pstm.Next();
		}
	}

	/// <summary>
	/// Simulates a C pointer to some data. Data is never copied,
	/// only the pointer is updated.
	/// </summary>
	internal class BufferPtr {

		private readonly byte[] _buf;
		private int _pos;

		public BufferPtr(byte[] buf, int pos = 0) {
			_buf = buf;
			_pos = pos;
		}

		public static BufferPtr FromPtr(BufferPtr ptr) {
			return new BufferPtr(ptr._buf, ptr._pos);
		}

		public void Incr(int offset = 1) {
			_pos += offset;
		}

		public void Decr(int offset = 1) {
			_pos -= offset;
		}

		public byte Get(int offset = -1) {
			return _buf[offset > -1 ? offset : _pos];
		}

		public byte[] GetMany(int length) {
			return _buf.Skip(_pos).Take(length).ToArray();
		}

		public byte Next() {
			return _buf[_pos++];
		}

		public void Set(byte value) {
			_buf[_pos] = value;
		}

		public int GetPos() {
			return _pos;
		}

		public byte[] GetBuffer() {
			return _buf;
		}
	}
}
