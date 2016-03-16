﻿/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Railgun
{
  internal static class RailEncoders
  {
    // Misc
    public static readonly IntEncoder Bit = new IntEncoder(0, 1);
    public static readonly BoolEncoder Bool = new BoolEncoder();

    // Special Types
    public static readonly TypedEncoder<EntityId> EntityId = new TypedEncoder<EntityId>();
    public static readonly TypedEncoder<Tick> Tick = new TypedEncoder<Tick>();
    internal static readonly TypedEncoder<EventId> EventId = new TypedEncoder<EventId>();
    internal static readonly TypedEncoder<TickSpan> TickSpan = new TypedEncoder<TickSpan>();

    // Types
    internal static readonly IntEncoder EntityType = new IntEncoder(0, 31);
    internal static readonly IntEncoder EventType = new IntEncoder(-10, 117);

    // Counts
    internal static readonly IntEncoder EntityCount = new IntEncoder(0, RailConfig.MAX_ENTITY_COUNT);
    internal static IntEncoder EventCount { get { return Railgun.EventId.CountEncoder; } }
    internal static readonly IntEncoder CommandCount = new IntEncoder(0, RailConfig.COMMAND_SEND_COUNT);
  }
}