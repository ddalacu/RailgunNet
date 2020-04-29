/*
 *  NetDemo - A Unity client/standalone server demo using Railgun and MiniUDP
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;

namespace Tests.Example
{
    public class Command : RailCommand<Command>
    {
        public float PosX;
        public float PosY;

        public void SetData(
            float X, float Y)
        {
            PosX = X;
            PosY = Y;
        }
        protected override void CopyDataFrom(Command other)
        {
            SetData(
                other.PosX, other.PosY);
        }

        protected override void DecodeData(RailBitBuffer buffer)
        {
            SetData(buffer.ReadFloat(Compression.Coordinate), buffer.ReadFloat(Compression.Coordinate));
        }

        protected override void ResetData()
        {
            SetData(0, 0);
        }

        protected override void EncodeData(RailBitBuffer buffer)
        {
            buffer.WriteFloat(Compression.Coordinate, PosX);
            buffer.WriteFloat(Compression.Coordinate, PosY);
        }
    }
}
