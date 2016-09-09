﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;

namespace Aura.Mabi
{
	public static class MabiMath
	{
		/// <summary>
		/// Converts Mabi's byte direction into a radian.
		/// </summary>
		/// <remarks>
		/// While entity packets use a byte from 0-255 for the direction,
		/// props are using radian floats.
		/// </remarks>
		/// <param name="direction"></param>
		/// <returns></returns>
		public static float ByteToRadian(byte direction)
		{
			return (float)Math.PI * 2 / 255 * direction;
		}

		/// <summary>
		/// Converts vector direction into Mabi's byte direction.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static byte DirectionToByte(double x, double y)
		{
			return (byte)(Math.Floor(Math.Atan2(y, x) / 0.02454369260617026));
		}

		/// <summary>
		/// Converts degree into Mabi's byte direction.
		/// </summary>
		/// <param name="degree"></param>
		/// <returns></returns>
		public static byte DegreeToByte(int degree)
		{
			return (byte)(degree * 255 / 360);
		}

		/// <summary>
		/// Converts vector direction into a radian.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static float DirectionToRadian(double x, double y)
		{
			return ByteToRadian(DirectionToByte(x, y));
		}

		/// <summary>
		/// Converts degree to radian.
		/// </summary>
		/// <param name="degree"></param>
		/// <returns></returns>
		public static float DegreeToRadian(int degree)
		{
			return (float)(Math.PI / 180f * degree);
		}

		/// <summary>
		/// Converts radian to byte.
		/// </summary>
		/// <param name="radian"></param>
		/// <returns></returns>
		public static byte RadianToByte(float radian)
		{
			var degree = (radian / Math.PI * 180f);
			return (byte)(degree * 255 / 360f);
		}

		/// <summary>
		/// Calculates the stat bonus for eating food.
		/// </summary>
		/// <remarks>
		/// Formula: (Stat Boost * Hunger Filled) / (Hunger Fill * 20 * Current Age of Character)
		/// Reference: http://wiki.mabinogiworld.com/view/Food_List
		/// </remarks>
		/// <param name="boost"></param>
		/// <param name="hunger"></param>
		/// <param name="hungerFilled"></param>
		/// <param name="age"></param>
		/// <returns></returns>
		public static float FoodStatBonus(double boost, double hunger, double hungerFilled, int age)
		{
			return (float)((boost * hungerFilled) / (hunger * 20 * age));
		}

		/// <summary>
		/// Returns the color for the name.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static int GetNameColor(string name)
		{
			var ccolor = new int[3];
			for (var i = 0; i < name.Length; i++)
				ccolor[i % 3] += name[i];
			for (var i = 0; i < ccolor.Length; i++)
				ccolor[i] = (ccolor[i] * 101) % 97 + 159;

			return (ccolor[0] << 16 | ccolor[1] << 8 | ccolor[2] << 0);
		}
	}
}
