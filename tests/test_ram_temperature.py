"""Prueba del parche 6 de la pantalla: temperatura de cada módulo de RAM (datos CUSTOM RamTemperature1/2).

LibreHardwareMonitor necesita permisos de administrador, así que se simula con objetos falsos.
Ejecutar con el Python del entorno de compilación, desde la raíz del repositorio:
    build\\venv-build\\Scripts\\python tests\\test_ram_temperature.py
"""
import math
import os
import sys
import types

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "turing-smart-screen-python"))

from library.sensors import sensors_custom as custom

LHM = "library.sensors.sensors_librehardwaremonitor"


class Sensor:
    def __init__(self, kind, value):
        self.SensorType, self.Value = kind, value


class Hw:
    def __init__(self, kind, name, sensors):
        self.HardwareType, self.Name, self.Sensors, self.updates = kind, name, sensors, 0

    def Update(self):
        self.updates += 1


def fake_lhm(hardware):
    module = types.ModuleType(LHM)
    module.Hardware = types.SimpleNamespace(HardwareType=types.SimpleNamespace(Memory="Memory", Cpu="Cpu"),
                                            SensorType=types.SimpleNamespace(Temperature="Temperature", Load="Load"))
    module.handle = types.SimpleNamespace(Hardware=hardware)
    sys.modules[LHM] = module
    return module


def equipo(t1=38.4, t2=39.6):
    return [
        Hw("Cpu", "AMD Ryzen 9 9950X3D", [Sensor("Temperature", 62.0)]),
        Hw("Memory", "Virtual Memory", [Sensor("Load", 40.0)]),
        Hw("Memory", "Total Memory", [Sensor("Load", 47.0)]),
        Hw("Memory", "Kingston - KF568C34-16 (#1)", [Sensor("Load", 1.0), Sensor("Temperature", t1)]),
        Hw("Memory", "Kingston - KF568C34-16 (#3)", [Sensor("Temperature", t2)]),
    ]


def test_sin_librehardwaremonitor_no_hay_dato():
    sys.modules.pop(LHM, None)
    assert custom.ram_module_temperatures() == []
    t = custom.RamTemperature1()
    assert math.isnan(t.as_numeric())
    assert t.as_string() == "--°C"


def test_dos_modulos():
    hw = equipo()
    fake_lhm(hw)
    assert custom.ram_module_temperatures() == [38.4, 39.6]
    t1, t2 = custom.RamTemperature1(), custom.RamTemperature2()
    assert t1.as_numeric() == 38.4 and t1.as_string() == "38°C"
    assert t2.as_numeric() == 39.6 and t2.as_string() == "40°C"
    assert hw[3].updates > 0 and hw[0].updates == 0  # solo se actualiza la memoria


def test_as_string_sin_as_numeric_previo():
    fake_lhm(equipo())
    assert custom.RamTemperature2().as_string() == "40°C"


def test_modulo_sin_lectura():
    fake_lhm(equipo(t2=None))  # el sensor existe pero aún no tiene valor
    assert custom.ram_module_temperatures() == [38.4]
    assert custom.RamTemperature1().as_string() == "38°C"
    assert custom.RamTemperature2().as_string() == "--°C"


if __name__ == "__main__":
    pruebas = [f for n, f in sorted(globals().items()) if n.startswith("test_")]
    for prueba in pruebas:
        prueba()
        print("OK  ", prueba.__name__)
    print(f"{len(pruebas)}/{len(pruebas)} pruebas superadas")
