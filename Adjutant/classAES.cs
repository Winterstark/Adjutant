using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adjutant
{
    class AES
    {
        #region boxes
        byte[,] Sbox = new byte[16, 16] {
        /* 0 1 2 3 4 5 6 7 8 9 a b c d e f */
        /*0*/ {0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76},
        /*1*/ {0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0},
        /*2*/ {0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15},
        /*3*/ {0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75},
        /*4*/ {0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84},
        /*5*/ {0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf},
        /*6*/ {0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8},
        /*7*/ {0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2},
        /*8*/ {0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73},
        /*9*/ {0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb},
        /*a*/ {0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79},
        /*b*/ {0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08},
        /*c*/ {0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a},
        /*d*/ {0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e},
        /*e*/ {0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf},
        /*f*/ {0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16} };

        byte[,] iSbox = new byte[16, 16]{
        /* 0 1 2 3 4 5 6 7 8 9 a b c d e f */
        /*0*/ {0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb},
        /*1*/ {0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb},
        /*2*/ {0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e},
        /*3*/ {0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25},
        /*4*/ {0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92},
        /*5*/ {0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84},
        /*6*/ {0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06},
        /*7*/ {0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b},
        /*8*/ {0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73},
        /*9*/ {0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e},
        /*a*/ {0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b},
        /*b*/ {0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4},
        /*c*/ {0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f},
        /*d*/ {0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef},
        /*e*/ {0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61},
        /*f*/ {0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d} };

        byte[,] Rcon = new byte[11, 4] { 
        {0x00, 0x00, 0x00, 0x00},
        {0x01, 0x00, 0x00, 0x00},
        {0x02, 0x00, 0x00, 0x00},
        {0x04, 0x00, 0x00, 0x00},
        {0x08, 0x00, 0x00, 0x00},
        {0x10, 0x00, 0x00, 0x00},
        {0x20, 0x00, 0x00, 0x00},
        {0x40, 0x00, 0x00, 0x00},
        {0x80, 0x00, 0x00, 0x00},
        {0x1b, 0x00, 0x00, 0x00},
        {0x36, 0x00, 0x00, 0x00} };

        byte[,] coef = new byte[4, 4] {
            {0x02, 0x03, 0x01, 0x01},
            {0x01, 0x02, 0x03, 0x01},
            {0x01, 0x01, 0x02, 0x03},
            {0x03, 0x01, 0x01, 0x02} };

        byte[,] invCoef = new byte[4, 4] {
            {0x0e, 0x0b, 0x0d, 0x09},
            {0x09, 0x0e, 0x0b, 0x0d},
            {0x0d, 0x09, 0x0e, 0x0b},
            {0x0b, 0x0d, 0x09, 0x0e} };
        #endregion

        public void Encrypt(string inputPath, string outputPath)
        {
            //input
            System.IO.BinaryReader inputFile = new System.IO.BinaryReader(new System.IO.FileStream(inputPath, System.IO.FileMode.Open));

            int lth = (int)inputFile.BaseStream.Length;
            int len = (lth / 16 + 1) * 16;

            byte[] input = new byte[len];
            byte[] output = new byte[len];

            for (int i = 0; i < lth; i++)
                input[i] = inputFile.ReadByte();

            byte padd;
            if (lth % 16 == 0)
                padd = 4;
            else
                padd = (byte)(16 - lth % 16);

            for (int i = lth; i < len; i++)
                input[i] = padd;

            inputFile.Close();

            //preparation
            int nk = 128 / 32;
            int nr = nk + 6;

            byte[] key = genKey(nk, nr);
            byte[] block = new byte[16];

            //aes crypt, ecb
            for (int i = 0; i < input.Length / 16; i++)
            {
                for (int j = 0; j < 16; j++)
                    block[j] = input[16 * i + j];

                for (int j = 0; j < 16; j++)
                    block[j] ^= key[j];

                for (int j = 0; j < nr - 1; j++)
                {
                    block = subWord(block, 16);
                    shiftRows(block);
                    block = mixCols(block);

                    for (int k = 0; k < 16; k++)
                        block[k] ^= key[k];
                }

                block = subWord(block, 16);
                shiftRows(block);

                for (int j = 0; j < 16; j++)
                    block[j] ^= key[j];

                //save block
                for (int j = 0; j < 16; j++)
                    output[16 * i + j] = block[j];
            }

            //output
            System.IO.BinaryWriter outputFile = new System.IO.BinaryWriter(new System.IO.FileStream(outputPath, System.IO.FileMode.Create));

            for (int i = 0; i < output.Length; i++)
                outputFile.Write(output[i]);

            outputFile.Close();
        }

        public void Decrypt(string inputPath, string outputPath)
        {
            //input
            System.IO.BinaryReader inputFile = new System.IO.BinaryReader(new System.IO.FileStream(inputPath, System.IO.FileMode.Open));

            byte[] input = new byte[inputFile.BaseStream.Length];
            byte[] output = new byte[inputFile.BaseStream.Length];

            for (int i = 0; i < input.Length; i++)
                input[i] = inputFile.ReadByte();

            inputFile.Close();

            //preparation
            int nk = 128 / 32;
            int nr = nk + 6;
            int pad = 0;
            bool isPadded;

            byte[] key = genKey(nk, nr);
            byte[] block = new byte[16];

            //aes decrypt, ecb
            for (int i = 0; i < input.Length / 16; i++)
            {
                for (int j = 0; j < 16; j++)
                    block[j] = input[16 * i + j];

                for (int j = 0; j < 16; j++)
                    block[j] ^= key[j];

                for (int j = 0; j < nr - 1; j++)
                {
                    invShiftRows(block);
                    block = invSubWord(block, 16);

                    for (int k = 0; k < 16; k++)
                        block[k] ^= key[k];

                    block = invMixCols(block);
                }

                invShiftRows(block);
                block = invSubWord(block, 16);

                for (int j = 0; j < 16; j++)
                    block[j] ^= key[j];

                //save block 
                if (i == input.Length / 16 - 1)
                {
                    isPadded = true;
                    for (int j = 0; j < 16; j++)
                        if (block[j] != 4)
                        {
                            isPadded = false;
                            break;
                        }

                    if (!isPadded)
                    {
                        pad = block[15];

                        for (int j = 0; j < 16 - pad; j++)
                            output[16 * i + j] = block[j];
                    }
                    else
                        pad = 16;
                }
                else
                    for (int j = 0; j < 16; j++)
                        output[16 * i + j] = block[j];

            }

            //output
            System.IO.BinaryWriter outputFile = new System.IO.BinaryWriter(new System.IO.FileStream(outputPath, System.IO.FileMode.Create));

            for (int i = 0; i < output.Length - pad; i++)
                outputFile.Write(output[i]);

            outputFile.Close();
        }

        private byte[] genKey(int nk, int nr)
        {
            byte[] pattern = { 4, 6, 8, 15, 16, 23, 42, 108 };
            byte[] key = new byte[4 * nk];

            for (int i = 0; i < 4 * nk; i++)
                key[i] = pattern[i % 8];

            byte[] expKey = new byte[(nr + 1) * 16];
            keyExpansion(key, expKey, nk);

            return expKey;
        }

        private void keyExpansion(byte[] key, byte[] w, int nk)
        {
            for (int i = 0; i < 4 * nk; i++)
                w[i] = key[i];

            int nr = nk + 6;
            byte[] temp = new byte[4];

            for (int i = nk; i < 4 * (nr + 1); i++)
            {
                for (int j = 0; j < 4; j++)
                    temp[j] = w[i * 4 - 1 + j];

                if (i % nk == 0)
                {
                    temp = subWord(rotWord(temp), 4);
                    for (int j = 0; j < 4; j++)
                        temp[j] ^= Rcon[i / nk, j];
                }
                else if (nk > 6 && i % nk == 4)
                    temp = subWord(temp, 4);

                for (int j = 0; j < 4; j++)
                    w[i * 4 + j] = (byte)(w[i * 4 - nk + j] ^ temp[j]);
            }
        }

        private byte[] rotWord(byte[] word)
        {
            byte[] newWord = new byte[4];

            newWord[0] = word[1];
            newWord[1] = word[2];
            newWord[2] = word[3];
            newWord[3] = word[0];

            return newWord;
        }

        private byte[] subWord(byte[] word, int n)
        {
            byte[] newWord = new byte[n];
            string hex = BitConverter.ToString(word).Replace("-", ""); ;

            for (int i = 0; i < n; i++)
                newWord[i] = Sbox[int.Parse(hex[2 * i].ToString(), System.Globalization.NumberStyles.HexNumber), int.Parse(hex[2 * i + 1].ToString(), System.Globalization.NumberStyles.HexNumber)];

            return newWord;
        }

        private byte[] invSubWord(byte[] word, int n)
        {
            byte[] newWord = new byte[n];
            string hex = BitConverter.ToString(word).Replace("-", ""); ;

            for (int i = 0; i < n; i++)
                newWord[i] = iSbox[int.Parse(hex[2 * i].ToString(), System.Globalization.NumberStyles.HexNumber), int.Parse(hex[2 * i + 1].ToString(), System.Globalization.NumberStyles.HexNumber)];

            return newWord;
        }

        private void shiftRows(byte[] block)
        {
            byte temp;

            for (int i = 1; i < 4; i++)
                for (int j = 0; j < i; j++)
                {
                    temp = block[4 * i];
                    for (int k = 0; k < 3; k++)
                        block[4 * i + k] = block[4 * i + k + 1];
                    block[4 * i + 3] = temp;
                }
        }

        private void invShiftRows(byte[] block)
        {
            byte temp;

            for (int i = 1; i < 4; i++)
                for (int j = 0; j < i; j++)
                {
                    temp = block[4 * i + 3];
                    for (int k = 3; k > 0; k--)
                        block[4 * i + k] = block[4 * i + k - 1];
                    block[4 * i] = temp;
                }
        }

        private byte[] mixCols(byte[] block)
        {
            byte[] newBlock = new byte[16];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    newBlock[i + 4 * j] = 0;

                    for (int k = 0; k < 4; k++)
                        newBlock[i + 4 * j] ^= polyMultiply(coef[j, k], block[i + 4 * k]);
                }
            }

            return newBlock;
        }

        private byte[] invMixCols(byte[] block)
        {
            byte[] newBlock = new byte[16];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    newBlock[i + 4 * j] = 0;

                    for (int k = 0; k < 4; k++)
                        newBlock[i + 4 * j] ^= polyMultiply(invCoef[j, k], block[i + 4 * k]);
                }
            }

            return newBlock;
        }

        private byte polyMultiply(byte x1, byte x2)
        {
            string a = Convert.ToString(x1, 2).PadLeft(8, '0'), b = Convert.ToString(x2, 2).PadLeft(8, '0');
            char[] c = new char[16];

            for (int i = 0; i < b.Length; i++)
                if (b[i] == '1')
                    for (int j = 0; j < a.Length; j++)
                        if (a[j] == '1')
                            c[14 - i - j] = (char)((c[14 - i - j] + 1) % 2);

            string g = "100011011";
            int div;
            char tmp;

            for (int i = 0; i < 8; i++)
            {
                tmp = c[i];
                c[i] = c[15 - i];
                c[15 - i] = tmp;
            }

            for (div = 0; div < c.Length; div++)
                if (c[div] == 1)
                    break;

            while (15 - div >= 8)
            {
                div = (15 - div) - 8;

                for (int j = 0; j < g.Length; j++)
                    if (g[j] == '1')
                        c[15 - (8 - j + div)] = (char)((c[15 - (8 - j + div)] + 1) % 2);

                for (div = 0; div < c.Length; div++)
                    if (c[div] == 1)
                        break;
            }

            byte x = 0;
            for (int i = 0; i < 16; i++)
                if (c[i] == 1)
                    x += (byte)Math.Pow(2, 15 - i);

            return x;
        }
    }
}
