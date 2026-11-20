"""Prueba del parche 4 de la pantalla: detectar si Motherboard Panel está en marcha (mutex con nombre).

Ejecutar con el Python del entorno de compilación, desde la raíz del repositorio:
    build\\venv-build\\Scripts\\python tests\\test_motherboard_panel.py
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "turing-smart-screen-python"))

import win32api
import win32event

from library.motherboard_panel import is_running

NAME = "MP.Test.X"


def test_sin_mutex_no_esta_en_marcha():
    assert is_running(NAME) is False


def test_con_mutex_esta_en_marcha():
    handle = win32event.CreateMutex(None, False, NAME)
    try:
        assert is_running(NAME) is True
    finally:
        win32api.CloseHandle(handle)


def test_al_cerrar_el_mutex_deja_de_estar_en_marcha():
    handle = win32event.CreateMutex(None, False, NAME)
    win32api.CloseHandle(handle)
    assert is_running(NAME) is False


def test_nombre_por_defecto_es_motherboard_panel():
    handle = win32event.CreateMutex(None, False, "MotherboardPanel")
    try:
        assert is_running() is True
    finally:
        win32api.CloseHandle(handle)


if __name__ == "__main__":
    pruebas = [f for n, f in sorted(globals().items()) if n.startswith("test_")]
    for prueba in pruebas:
        prueba()
        print("OK  ", prueba.__name__)
    print(f"{len(pruebas)}/{len(pruebas)} pruebas superadas")
