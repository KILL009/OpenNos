﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using System;
using AutoMapper;
using OpenNos.Data;

namespace OpenNos.GameObject
{
    public class MagicalItem : Item
    {
        public override void Use(ClientSession Session)
        {
            MagicalItemHandler instance = new MagicalItemHandler();
<<<<<<< HEAD
            instance.UseItemHandler(Session, this, Effect, EffectValue);
=======
            instance.UseItemHandler(Session, Effect, EffectValue);
>>>>>>> fceff5cac1472390cb1e55e204bf4489449ceb90
        }
        #region Instantiation

        #endregion

        #region Methods

<<<<<<< HEAD
       
=======

>>>>>>> fceff5cac1472390cb1e55e204bf4489449ceb90
        #endregion
    }
}