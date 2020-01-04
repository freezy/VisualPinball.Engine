namespace VisualPinball.Engine.IO
{
	public class LzwReader
	{

		const int MAX_CODES = 4095;
		private int[] CODE_MASK = {
			0,
			0x0001, 0x0003,
			0x0007, 0x000F,
			0x001F, 0x003F,
			0x007F, 0x00FF,
			0x01FF, 0x03FF,
			0x07FF, 0x0FFF
		};

		private BufferPtr pstm;

		/* output */
		private readonly BufferPtr pbBitsOutCur;
		private readonly int cbStride;
		private int badCodeCount;

		/* Static variables */
		private int currSize = 0;                 /* The current code size */
		private int clear = 0;                    /* Value for a clear code */
		private int ending = 0;                   /* Value for a ending code */
		private int newCodes = 0;                 /* First available code */
		private int topSlot = 0;                  /* Highest code for current size */
		private int slot = 0;                     /* Last read code */

		/* The following static variables are used
		 * for separating out codes
		 */
		private int numAvailBytes = 0;              /* # bytes left in block */
		private int numBitsLeft = 0;                /* # bits left in current byte */
		private byte b1 = 0x0;                         /* Current byte */
		private byte[] byteBuff = new byte[257];      /* Current block */
		private BufferPtr pBytes;                     /* points to byte_buff - Pointer to next byte in block */

		private byte[] stack = new byte[MAX_CODES + 1];     /* Stack for storing pixels */
		private byte[] suffix = new byte[MAX_CODES + 1];    /* Suffix table */
		private int[] prefix = new int[MAX_CODES + 1];                        /* Prefix linked list */

		private readonly int width;
		private readonly int height;
		private int linesLeft;

		LzwReader(byte[] pstm, int width, int height, int pitch) {
			for (var i = 0; i < MAX_CODES + 1; i++) {
				this.prefix[i] = 0;
			}
			this.cbStride = pitch;
			this.pbBitsOutCur = new BufferPtr(new byte[pitch * height]);

			this.badCodeCount = 0;

			this.pstm = new BufferPtr(pstm);

			this.width = width; // 32-bit picture
			this.height = height;
			this.linesLeft = height + 1; // +1 because 1 gets taken o
		}

		public void decompress(out byte[] data, out int length) {

			BufferPtr sp; // points to this.stack
			BufferPtr bufPtr; // points to this.buf
			BufferPtr buf;
			int bufCnt;

			int c;
			int oc;
			int fc;
			int code;
			int size;

			/* Initialize for decoding a new image...
			 */
			size = 8;
			this.initExp(size);

			/* Initialize in case they forgot to put in a clear code.
			 * (This shouldn't happen, but we'll try and decode it anyway...)
			 */
			oc = fc = 0;

			/* Allocate space for the decode buffer
			 */
			buf = this.NextLine();

			/* Set up the stack pointer and decode buffer pointer
			 */
			sp = new BufferPtr(this.stack);
			bufPtr = BufferPtr.fromPtr(buf);
			bufCnt = this.width;

			/* This is the main loop.  For each code we get we pass through the
			 * linked list of prefix codes, pushing the corresponding "character" for
			 * each code onto the stack.  When the list reaches a single "character"
			 * we push that on the stack too, and then start unstacking each
			 * character for output in the correct order.  Special handling is
			 * included for the clear code, and the whole thing ends when we get
			 * an ending code.
			 */
			c = this.getNextCode();
			while (c != this.ending) {

				/* If we had a file error, return without completing the decode
				 */
				if (c < 0) {
					break;
				}

				/* If the code is a clear code, reinitialize all necessary items.
				 */
				if (c == this.clear) {
					this.currSize = size + 1;
					this.slot = this.newCodes;
					this.topSlot = 1 << this.currSize;

					/* Continue reading codes until we get a non-clear code
					 * (Another unlikely, but possible case...)
					 */
					c = this.getNextCode();
					while (c == this.clear) {
						c = this.getNextCode();
					}

					/* If we get an ending code immediately after a clear code
					 * (Yet another unlikely case), then break out of the loop.
					 */
					if (c == this.ending) {
						break;
					}

					/* Finally, if the code is beyond the range of already set codes,
					 * (This one had better NOT happen...  I have no idea what will
					 * result from this, but I doubt it will look good...) then set it
					 * to color zero.
					 */
					if (c >= this.slot) {
						c = 0;
					}

					oc = fc = c;

					/* And var us not forget to put the char into the buffer... And
					 * if, on the off chance, we were exactly one pixel from the end
					 * of the line, we have to send the buffer to the out_line()
					 * routine...
					 */
					bufPtr.set((byte)c);
					bufPtr.incr();

					if (--bufCnt == 0) {
						buf = this.NextLine();
						bufPtr = BufferPtr.fromPtr(buf);
						bufCnt = this.width;
					}

				} else {

					/* In this case, it's not a clear code or an ending code, so
					 * it must be a code code...  So we can now decode the code into
					 * a stack of character codes. (Clear as mud, right?)
					 */
					code = c;

					/* Here we go again with one of those off chances...  If, on the
					 * off chance, the code we got is beyond the range of those already
					 * set up (Another thing which had better NOT happen...) we trick
					 * the decoder into thinking it actually got the last code read.
					 * (Hmmn... I'm not sure why this works...  But it does...)
					 */
					if (code >= this.slot) {
						if (code > this.slot) {
							++this.badCodeCount;
						}
						code = oc;
						sp.set((byte)fc);
						sp.incr();
					}

					/* Here we scan back along the linked list of prefixes, pushing
					 * helpless characters (ie. suffixes) onto the stack as we do so.
					 */
					while (code >= this.newCodes) {
						sp.set(this.suffix[code]);
						sp.incr();
						code = this.prefix[code];
					}

					/* Push the last character on the stack, and set up the new
					 * prefix and suffix, and if the required slot number is greater
					 * than that allowed by the current bit size, increase the bit
					 * size.  (NOTE - If we are all full, we *don't* save the new
					 * suffix and prefix...  I'm not certain if this is correct...
					 * it might be more proper to overwrite the last code...
					 */
					sp.set((byte)code);
					sp.incr();
					if (this.slot < this.topSlot) {
						fc = code;
						this.suffix[this.slot] = (byte)fc;	// = code;
						this.prefix[this.slot++] = oc;
						oc = c;
					}
					if (this.slot >= this.topSlot) {
						if (this.currSize < 12) {
							this.topSlot <<= 1;
							++this.currSize;
						}
					}

					/* Now that we've pushed the decoded string (in reverse order)
					 * onto the stack, lets pop it off and put it into our decode
					 * buffer...  And when the decode buffer is full, write another
					 * line...
					 */
					while (sp.getPos() > 0) {

						sp.decr();
						bufPtr.set(sp.get());
						bufPtr.incr();
						if (--bufCnt == 0) {
							buf = this.NextLine();
							bufPtr = buf;
							bufCnt = this.width;
						}
					}
				}
				c = this.getNextCode();
			}

			data = this.pbBitsOutCur.getBuffer();
			length = this.pstm.getPos();
		}

		private void initExp(int size) {
			this.currSize = size + 1;
			this.topSlot = 1 << this.currSize;
			this.clear = 1 << size;
			this.ending = this.clear + 1;
			this.slot = this.newCodes = this.ending + 1;
			this.numAvailBytes = this.numBitsLeft = 0;
		}

		private BufferPtr NextLine() {
			var pbRet = BufferPtr.fromPtr(this.pbBitsOutCur);
			this.pbBitsOutCur.incr(this.cbStride);	// fucking upside down dibs!
			this.linesLeft--;
			return pbRet;
		}

		private int getNextCode() {
			int ret;
			if (this.numBitsLeft == 0) {
				if (this.numAvailBytes <= 0) {

					/* Out of bytes in current block, so read next block
					 */
					this.pBytes = new BufferPtr(this.byteBuff);
					this.numAvailBytes = this.getByte();
					if (this.numAvailBytes < 0) {
						return (this.numAvailBytes);

					} else if (this.numAvailBytes > 0) {
						for (var i = 0; i < this.numAvailBytes; ++i) {
							var x = this.getByte();
							if (x < 0) {
								return x;
							}
							this.byteBuff[i] = (byte)x;
						}
					}
				}
				this.b1 = this.pBytes.get();
				this.pBytes.incr();
				this.numBitsLeft = 8;
				--this.numAvailBytes;
			}

			ret = this.b1 >> (8 - this.numBitsLeft);
			while (this.currSize > this.numBitsLeft) {
				if (this.numAvailBytes <= 0) {

					/* Out of bytes in current block, so read next block
					 */
					this.pBytes = new BufferPtr(this.byteBuff);
					this.numAvailBytes = this.getByte();
					if (this.numAvailBytes < 0) {
						return this.numAvailBytes;

					} else if (this.numAvailBytes > 0) {
						for (var i = 0; i < this.numAvailBytes; ++i) {
							var x = this.getByte();
							if (x < 0) {
								return x;
							}
							this.byteBuff[i] = (byte)x;
						}
					}
				}
				this.b1 = this.pBytes.get();
				this.pBytes.incr();
				ret |= this.b1 << this.numBitsLeft;
				this.numBitsLeft += 8;
				--this.numAvailBytes;
			}
			this.numBitsLeft -= this.currSize;
			ret &= CODE_MASK[this.currSize];
			return ret;
		}

		private int getByte() {
			return this.pstm.next();
		}
	}


	/**
 * Simulates a C pointer to some data. Data is never copied,
 * only the pointer is updated.
 */
	internal class BufferPtr {

		private readonly byte[] buf;
		private int pos;

		public BufferPtr(byte[] buf, int pos = 0) {
			this.buf = buf;
			this.pos = pos;
		}

		public static BufferPtr fromPtr(BufferPtr ptr) {
			return new BufferPtr(ptr.buf, ptr.pos);
		}

		public void incr(int offset = 1) {
			this.pos += offset;
		}

		public void decr(int offset = 1) {
			this.pos -= offset;
		}

		public byte get(int offset = -1) {
			return this.buf[offset > -1 ? offset : this.pos];
		}

		public byte next() {
			return this.buf[this.pos++];
		}

		public void set(byte value) {
			this.buf[this.pos] = value;
		}

		public int getPos() {
			return this.pos;
		}

		public byte[] getBuffer() {
			return this.buf;
		}
	}

}
