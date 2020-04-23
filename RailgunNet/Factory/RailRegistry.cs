﻿/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
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
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public class RailRegistry
    {
        private readonly List<KeyValuePair<Type, Type>> entityTypes;
        private readonly List<Type> eventTypes;

        public RailRegistry(Component eComponent)
        {
            Component = eComponent;
            CommandType = null;
            eventTypes = new List<Type>();
            entityTypes = new List<KeyValuePair<Type, Type>>();
        }

        public Component Component { get; }
        public Type CommandType { get; private set; }

        public IEnumerable<Type> EventTypes => eventTypes;

        public IEnumerable<KeyValuePair<Type, Type>> EntityTypes => entityTypes;

        [PublicAPI]
        public void SetCommandType<TCommand>()
            where TCommand : RailCommand
        {
            CommandType = typeof(TCommand);
        }

        [PublicAPI]
        public void AddEventType<TEvent>()
            where TEvent : RailEvent
        {
            eventTypes.Add(typeof(TEvent));
        }

        [PublicAPI]
        public void AddEntityType<TEntity, TState>()
            where TEntity : RailEntity
            where TState : RailState
        {
            entityTypes.Add(
                new KeyValuePair<Type, Type>(typeof(TEntity), typeof(TState)));
        }
    }
}