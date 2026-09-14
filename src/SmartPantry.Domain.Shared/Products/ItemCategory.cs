using System;
using System.Collections.Generic;
using System.Text;

namespace SmartPantry.Products;
public enum ItemCategory
{
    Undefined = 0,
    Dairy,              // Lácteos
    Bakery,             // Panadería
    Beverages,          // Bebidas
    CannedGoods,        // Enlatados / Conservas
    DryGoods,           // Granos, pastas, harinas, legumbres
    FruitsAndVegetables,// Frutas y verduras
    MeatAndFish,        // Carnes y pescados
    Snacks,             // Snacks y golosinas
    Condiments,         // Condimentos, especias y salsas
    Frozen,             // Congelados
    Cleaning,           // Limpieza
    Other               // Otros
}