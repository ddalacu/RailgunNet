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
  /// <summary>
  /// Responsible for encoding and decoding snapshots.
  /// 
  /// Encoding order:
  /// | FRAME | IMAGE COUNT | ----- IMAGE ----- | ----- IMAGE ----- | ...
  /// 
  /// Image encoding order:
  /// If new: | ID | TYPE | ----- STATE DATA ----- |
  /// If old: | ID | ----- STATE DATA ----- |
  /// </summary>
  internal class Interpreter
  {
    /// <summary>
    /// Incorporates any non-updated entities from the basis snapshot into
    /// the newly-populated snapshot.
    /// </summary>
    private static void ReconcileBasis(Snapshot snapshot, Snapshot basis)
    {
      foreach (Image basisImage in basis.GetImages())
        if (snapshot.Contains(basisImage.Id) == false)
          snapshot.Add(basisImage.Clone());
    }

    private Pool<Snapshot> snapshotPool;
    private Pool<Image> imagePool;
    private Dictionary<int, Factory> stateFactories;

    internal Interpreter(params Factory[] factories)
    {
      this.snapshotPool = new Pool<Snapshot>();
      this.imagePool = new Pool<Image>();

      this.stateFactories = new Dictionary<int, Factory>();
      foreach (Factory factory in factories)
        this.stateFactories[factory.Type] = factory;
    }

    internal void Link(Environment environment)
    {
      ((IPoolable)environment).Pool = this.snapshotPool;
    }

    internal void Link(Entity entity)
    {
      ((IPoolable)entity).Pool = this.imagePool;
    }

    internal State CreateEmptyState(int type)
    {
      return this.stateFactories[type].Allocate();
    }

    internal void Encode(BitPacker bitPacker, Snapshot snapshot)
    {
      //UnityEngine.Debug.LogWarning("[Encode Full]");
      foreach (Image image in snapshot.GetImages())
      {
        // Write: [Image Data]
        this.EncodeImage(bitPacker, image);

        // Write: [Id]
        //UnityEngine.Debug.LogWarning("Writing Id: " + image.Id);
        bitPacker.Push(Encoders.EntityId, image.Id);
      }

      // Write: [Count]
      //UnityEngine.Debug.LogWarning("Writing Count: " + snapshot.Count);
      bitPacker.Push(Encoders.EntityCount, snapshot.Count);

      // Write: [Frame]
      //UnityEngine.Debug.LogWarning("Writing Frame: " + snapshot.Frame);
      bitPacker.Push(Encoders.Frame, snapshot.Frame);
    }

    internal void Encode(
      BitPacker bitPacker, 
      Snapshot snapshot, 
      Snapshot basis)
    {
      //UnityEngine.Debug.LogWarning("[Encode Delta]");
      int count = 0;

      foreach (Image image in snapshot.GetImages())
      {
        // Write: [Image Data]
        Image basisImage;
        if (basis.TryGet(image.Id, out basisImage))
        {
          if (this.EncodeImage(bitPacker, image, basisImage) == false)
            continue;
        }
        else
        {
          this.EncodeImage(bitPacker, image);
        }

        // We may not write every state
        count++;

        // Write: [Id]
        //UnityEngine.Debug.LogWarning("Writing Id: " + image.Id);
        bitPacker.Push(Encoders.EntityId, image.Id);
      }

      // Write: [Count]
      //UnityEngine.Debug.LogWarning("Writing Count: " + count);
      bitPacker.Push(Encoders.EntityCount, count);

      // Write: [Frame]
      //UnityEngine.Debug.LogWarning("Writing Frame: " + snapshot.Frame);
      bitPacker.Push(Encoders.Frame, snapshot.Frame);
    }

    internal Snapshot Decode(BitPacker bitPacker)
    {
      //UnityEngine.Debug.LogWarning("[Decode Full]");

      // Read: [Frame]
      int frame = bitPacker.Pop(Encoders.Frame);
      //UnityEngine.Debug.LogWarning("Reading Frame: " + frame);

      // Read: [Count]
      int count = bitPacker.Pop(Encoders.EntityCount);
      //UnityEngine.Debug.LogWarning("Reading Count: " + count);

      Snapshot snapshot = this.snapshotPool.Allocate();
      snapshot.Frame = frame;

      for (int i = 0; i < count; i++)
      {
        // Read: [Id]
        int imageId = bitPacker.Pop(Encoders.EntityId);
        //UnityEngine.Debug.LogWarning("Reading Id: " + imageId);

        // Read: [Image Data]
        snapshot.Add(this.DecodeImage(bitPacker, imageId));
      }

      return snapshot;
    }

    internal Snapshot Decode(BitPacker bitPacker, Snapshot basis)
    {
      //UnityEngine.Debug.LogWarning("[Decode Delta]");

      // Read: [Frame]
      int frame = bitPacker.Pop(Encoders.Frame);
      //UnityEngine.Debug.LogWarning("Reading Frame: " + frame);

      // Read: [Count]
      int count = bitPacker.Pop(Encoders.EntityCount);
      //UnityEngine.Debug.LogWarning("Reading Count: " + count);

      Snapshot snapshot = this.snapshotPool.Allocate();
      snapshot.Frame = frame;

      for (int i = 0; i < count; i++)
      {
        // Read: [Id]
        int imageId = bitPacker.Pop(Encoders.EntityId);
        //UnityEngine.Debug.LogWarning("Reading Id: " + imageId);

        // Read: [Image Data]
        Image basisImage;
        if (basis.TryGet(imageId, out basisImage))
          snapshot.Add(this.DecodeImage(bitPacker, imageId, basisImage));
        else
          snapshot.Add(this.DecodeImage(bitPacker, imageId));
      }

      Interpreter.ReconcileBasis(snapshot, basis);
      return snapshot;
    }

    private void EncodeImage(BitPacker bitPacker, Image image)
    {
      // Write: [State Data]
      //UnityEngine.Debug.LogWarning("Writing State Data...");
      image.State.Encode(bitPacker);

      // Write: [Type]
      //UnityEngine.Debug.LogWarning("Writing Type: " + image.State.Type);
      bitPacker.Push(Encoders.StateType, image.State.Type);
    }

    private bool EncodeImage(BitPacker bitPacker, Image image, Image basis)
    {
      // Write: [State Data]
      //UnityEngine.Debug.LogWarning("Writing State Data...");
      return image.State.Encode(bitPacker, basis.State);

      // (No type identifier for delta images)
    }

    private Image DecodeImage(BitPacker bitPacker, int imageId)
    {
      // Read: [Type]
      int stateType = bitPacker.Pop(Encoders.StateType);
      //UnityEngine.Debug.LogWarning("Reading Type: " + stateType);

      Image image = this.imagePool.Allocate();
      State state = this.stateFactories[stateType].Allocate();

      // Read: [State Data]
      state.Decode(bitPacker);
      //UnityEngine.Debug.LogWarning("Reading State Data...");

      image.Id = imageId;
      image.State = state;
      return image;
    }

    private Image DecodeImage(BitPacker bitPacker, int imageId, Image basis)
    {
      // (No type identifier for delta images)

      Image image = this.imagePool.Allocate();
      State state = this.stateFactories[basis.State.Type].Allocate();

      // Read: [State Data]
      state.Decode(bitPacker, basis.State);
      //UnityEngine.Debug.LogWarning("Reading State Data...");

      image.Id = imageId;
      image.State = state;
      return image;
    }
  }
}