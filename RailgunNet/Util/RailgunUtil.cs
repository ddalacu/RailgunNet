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

using UnityEngine;

namespace Railgun
{
  public static class RailgunUtil
  {
    public static void Swap<T>(ref T a, ref T b)
    {
      T temp = b;
      b = a;
      a = temp;
    }

    internal static void ExpandArray<T>(ref T[] oldArray)
    {
      // TODO: Revisit this using next-largest primes like built-in lists do
      int newCapacity = oldArray.Length * 2;
      T[] newArray = new T[newCapacity];
      Array.Copy(oldArray, newArray, oldArray.Length);
      oldArray = newArray;
    }

    #region Debug
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Assert(bool condition)
    {
      System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
      if (condition == false)
        UnityEngine.Debug.LogError("Assert failed\n" + t);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Assert(bool condition, object message)
    {
      System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
      if (condition == false)
        UnityEngine.Debug.LogError(message + "\n" + t);
    }

    public static void Log(object message)
    {
      UnityEngine.Debug.Log(message);
    }
    #endregion
  }
}
