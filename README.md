# Cedula-Uruguaya-con-SmartCard-.NET
Leer datos de la Cedula Uruguaya usando un SmartCard Reader y C# .NET Framework 4.0

Como el titulo lo intuye, este proyecto tiene como objetivo extraer los datos de la Cedula de Identidad Uruguaya (eID) usando un lector de Tarjetas inteligentes (SmartCard) y .NET Framework.

El lector de Tarjetas esta basado en este Proyecto:  https://www.codeproject.com/Articles/16653/A-Smart-Card-Framework-for-NET

El modo de leer la Cedula viene de aqui:   https://centroderecursos.agesic.gub.uy/web/seguridad/wiki/-/wiki/Main/Gu%C3%ADa+de+uso+de+CI+electr%C3%B3nica+a+trav%C3%A9s+de+APDU

He modificado bastante el proyecto original del SmartCard Reader, lo he arreglado para que funcione correctamente en ambientes de 64 bits, lo he probado extensamente en Windows 10 x64.

La Solucion está hecha con Visual Studio 2017 y .NET Framework 4.0 (para asegurar compatibilidad con Windows XP).

El lector SmartCard es independiente del lector de Cedula y puede usarse para leer cualquier tipo de Tarjeta.

El lector de Cedula está hecho solo para Lectores con Contacto ya que lo que se lee es el chip de la tarjeta.
Intente hacer una lectura con el lector Contacless pero es muy complicado y me quedé sin tiempo, mas adelante talvez lo suba.
