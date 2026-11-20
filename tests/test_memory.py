"""Prueba del parche 5 de la pantalla: devolver a Windows la memoria que el proceso no usa.

Ejecutar con el Python del entorno de compilación, desde la raíz del repositorio:
    build\\venv-build\\Scripts\\python tests\\test_memory.py
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "turing-smart-screen-python"))

import psutil

from library.memory import trim_working_set


def test_recorte_correcto():
    assert trim_working_set() is True


def test_recorte_baja_el_working_set():
    reserva = bytearray(64 * 1024 * 1024)  # 64 MB tocados: entran en el working set
    for i in range(0, len(reserva), 4096):
        reserva[i] = 1
    antes = psutil.Process().memory_info().rss
    assert trim_working_set() is True
    despues = psutil.Process().memory_info().rss
    assert despues < antes - 32 * 1024 * 1024, (antes, despues)
    del reserva


if __name__ == "__main__":
    pruebas = [f for n, f in sorted(globals().items()) if n.startswith("test_")]
    for prueba in pruebas:
        prueba()
        print("OK  ", prueba.__name__)
    print(f"{len(pruebas)}/{len(pruebas)} pruebas superadas")
