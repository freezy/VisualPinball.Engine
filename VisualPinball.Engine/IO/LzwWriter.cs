// GIF Image compression - modified 'compress'
//
// Based on: compress.c - File compression ala IEEE Computer, June 1984.
//
// By Authors:  Spencer W. Thomas      (decvax!harpo!utah-cs!utah-gr!thomas)
//              Jim McKie              (decvax!mcvax!jim)
//              Steve Davies           (decvax!vax135!petsd!peora!srd)
//              Ken Turkowski          (decvax!decwrl!turtlevax!ken)
//              James A. Woods         (decvax!ihnp4!ames!jaw)
//              Joe Orost              (decvax!vax135!petsd!joe)

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VisualPinball.Engine.IO
{
	public class LzwWriter
	{
		private const int Bits = 12;
		private const int MaxBits = Bits;
		private const int MaxMaxCode = 1 << Bits;
		private const int HSize = 5003; // 80% occupancy
		private const int GifEof = -1;

		private readonly BinaryWriter _writer;
		private readonly byte[] _bits;
		private readonly int _width;
		private readonly int _height;
		private readonly int _pitch; // x-length of each scan line (divisible by 8, normally)

		private int _nBits;
		private int _maxCode;

		private readonly int[] _hTab = new int[HSize];
		private readonly int[] _codeTab = new int[HSize];

		private int _freeEnt;

		private int _initBits;

		private int _clearCode;
		private int _eofCode;

		private int _curAccum;
		private int _curBits;

		private int _aCount;

		private readonly byte[] _accum = new byte[256];

		private int _iPixelCur;
		private int _iXCur;

		private bool _clearFlg;

		private static readonly int[] Masks = {
			0x0000, 0x0001, 0x0003, 0x0007, 0x000F,
			0x001F, 0x003F, 0x007F, 0x00FF,
			0x01FF, 0x03FF, 0x07FF, 0x0FFF,
			0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF
		};

		public LzwWriter(BinaryWriter writer, byte[] bits, int width, int height, int pitch)
		{
			_writer = writer;
			_bits = bits;
			_width = width;
			_height = height;
			_pitch = pitch;
		}

		private static int MaxCode(int nBits)
		{
			return (1 << nBits) - 1;
		}

		private void WriteSz(IEnumerable<byte> sz, int numBytes)
		{
			_writer.Write(sz.Take(numBytes).ToArray());
		}

		private void WriteByte(byte ch)
		{
			_writer.Write(ch);
		}

		private int NextPixel()
		{
			if (_iPixelCur == _pitch * _height) {
				return GifEof;
			}

			var ch = _bits[_iPixelCur];
			++_iPixelCur;
			++_iXCur;
			if (_iXCur == _width) {
				_iPixelCur += _pitch - _width;
				_iXCur = 0;
			}
			return ch;
		}

		public void CompressBits(int initBits)
		{
			int c;

			// Used to be in write gif
			_iPixelCur = 0;
			_iXCur = 0;

			_clearFlg = false;

			_curAccum = 0;
			_curBits = 0;

			_aCount = 0;

			// Set up the globals:  g_init_bits - initial number of bits
			// bits per pixel
			_initBits = initBits;

			// Set up the necessary values
			_nBits = _initBits;
			_maxCode = MaxCode(_nBits);

			_clearCode = 1 << (initBits - 1);
			_eofCode = _clearCode + 1;
			_freeEnt = _clearCode + 2;

			var ent = NextPixel();

			var hShift = 0;
			for (var fCode = HSize; fCode < 65536; fCode *= 2) {
				++hShift;
			}

			hShift = 8 - hShift;                           // set hash code range bound

			const int hSizeReg = HSize;
			ClearHash(hSizeReg);                           // clear hash table

			Output(_clearCode);

			while ((c = NextPixel()) != GifEof)
			{
				var fCode = (c << MaxBits) + ent;
				var i = (c << hShift) ^ ent;           // xor hashing

				if (_codeTab[i] != 0) {                    // is first probed slot empty?
					if (_hTab[i] == fCode) {
						ent = _codeTab[i];
						goto nextByte;
					}

					int disp;
					if (i == 0) {                          // secondary hash (after G. Knott)
						disp = 1;

					} else {
						disp = HSize - i;
					}

					while (true) {
						i -= disp;
						if (i < 0) {
							i += HSize;
						}

						if (_codeTab[i] == 0) {            // hit empty slot
							goto processByte;
						}

						if (_hTab[i] == fCode) {
							ent = _codeTab[i];
							goto nextByte;
						}
					}
				}

				processByte:
					Output(ent);
					ent = c;
					if (_freeEnt < MaxMaxCode) {
						_codeTab[i] = _freeEnt++;          // code -> hashtable
						_hTab[i] = fCode;
					} else {
						ClearBlock();
					}

				nextByte:
					;
			}
			// Put out the final code.
			Output(ent);
			Output(_eofCode);
		}

		private void Output(int code)
		{
			_curAccum &= Masks[_curBits];

			if (_curBits > 0) {
				_curAccum |= code << _curBits;

			} else {
				_curAccum = code;
			}

			_curBits += _nBits;

			while (_curBits >= 8) {
				CharOut((byte)(_curAccum & 0xff));
				_curAccum >>= 8;
				_curBits -= 8;
			}

			// If the next entry is going to be too big for the code size,
			// then increase it, if possible.
			if (_freeEnt > _maxCode || _clearFlg) {
				if (_clearFlg) {
					_maxCode = MaxCode(_nBits = _initBits);
					_clearFlg = false;

				} else {
					++_nBits;
					_maxCode = _nBits == MaxBits ? MaxMaxCode : MaxCode(_nBits);
				}
			}

			if (code == _eofCode) {
				// At EOF, write the rest of the buffer.
				while (_curBits > 0) {
					CharOut((byte)(_curAccum & 0xff));
					_curAccum >>= 8;
					_curBits -= 8;
				}
				FlushChar();
			}
		}

		private void ClearBlock()
		{
			ClearHash(HSize);
			_freeEnt = _clearCode + 2;
			_clearFlg = true;

			Output(_clearCode);
		}

		private void ClearHash(int hSize)
		{
			for (var i = 0; i < HSize; ++i) {
				_hTab[i] = -1;
				_codeTab[i] = 0;
			}
		}

		private void CharOut(byte c)
		{
			_accum[_aCount++] = c;
			if (_aCount >= 254) {
				FlushChar();
			}
		}

		private void FlushChar()
		{
			if (_aCount > 0) {
				WriteByte((byte)_aCount);
				WriteSz(_accum, _aCount);
				_aCount = 0;
			}
		}
	}
}
