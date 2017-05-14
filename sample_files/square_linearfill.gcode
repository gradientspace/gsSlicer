M136 (enable build)
M73 P0
G162 X Y F2000(home XY axes maximum)
G161 Z F900(home Z axis minimum)
G92 X0 Y0 Z-5 A0 B0 (set Z to -5)
G1 Z0.0 F900(move Z to '0')
G161 Z F100(home Z axis minimum)
M132 X Y Z A B (Recall stored home offsets for XYZAB axis)
G92 X152 Y75 Z0 A0 B0
G1 X-141 Y-74 Z40 F3300.0 (move to waiting position)
G130 X20 Y20 A20 B20 (Lower stepper Vrefs while heating)
M135 T0
M104 S230 T0
M133 T0
G130 X127 Y127 A127 B127 (Set Stepper motor Vref to defaults)
; Makerbot Industries
; Miracle Grue 3.9.4
; This file contains digital fabrication directives
; in G-Code format for your 3D printer
; http://www.makerbot.com/support/makerware/documentation/slicer/
; 
; Right Toolhead Weight (grams): 0.103757 
; Right Toolhead Distance (mm): 34.0062 
; Duration: 58.9226 seconds 
; Active extruders in print: 0 
; Chunk 0 
; Lower Position  0 
; Upper Position  0.2 
; Thickness       0.2 
; Width           0.4 
G1 X105.400 Y-74.000 Z0.270 F9000.000 (Extruder Prime Dry Move)
G1 X-141 Y-74 Z0.270 F1800.000 E25.000 (Extruder Prime Start)
G92 A0 B0 (Reset after prime)
G1 Z0.000000 F1000
G1 X-141.0 Y-74.0 Z0.0 F1000 E0.0
G92 E0
G1 X-141.000 Y-74.000 Z0.000 F1500 A-1.30000; Retract 
G1 X-141.000 Y-74.000 Z0.000 F3000; Retract 
G1 X-141.000 Y-74.000 Z0.200 F1380; Travel Move 
M73 P0; Update Progress 
G1 X-9.400 Y-9.400 Z0.200 F9000; Travel Move 
M73 P2; Update Progress 
G1 X-9.400 Y-9.400 Z0.200 F1500 A0.00000; Restart 
G1 X-9.400 Y9.400 Z0.200 F1800 A0.65725; Inset 
M73 P4; Update Progress 
G1 X9.400 Y9.400 Z0.200 F1800 A1.31449; Inset 
M73 P6; Update Progress 
G1 X9.400 Y-9.400 Z0.200 F1800 A1.97174; Inset 
M73 P8; Update Progress 
G1 X-9.400 Y-9.400 Z0.200 F1800 A2.62898; Inset 
M73 P10; Update Progress 
G1 X-9.400 Y-9.400 Z0.200 F1500 A1.32898; Retract 
G1 X-9.400 Y-9.400 Z0.200 F3000; Retract 
G1 X-9.800 Y-9.800 Z0.200 F9000; Travel Move 
G1 X-9.800 Y-9.800 Z0.200 F1500 A2.62898; Restart 
G1 X-9.800 Y9.800 Z0.200 F1800 A3.31420; Inset 
M73 P11; Update Progress 
G1 X9.800 Y9.800 Z0.200 F1800 A3.99941; Inset 
M73 P13; Update Progress 
G1 X9.800 Y-9.800 Z0.200 F1800 A4.68462; Inset 
M73 P15; Update Progress 
G1 X-9.800 Y-9.800 Z0.200 F1800 A5.36984; Inset 
M73 P17; Update Progress 
G1 X-9.800 Y-9.800 Z0.200 F1500 A4.06984; Retract 
G1 X-9.800 Y-9.800 Z0.200 F3000; Retract 
G1 X-8.994 Y-9.114 Z0.200 F9000; Travel Move 
G1 X-8.994 Y-9.114 Z0.200 F1500 A5.36984; Restart 
G1 X-8.994 Y9.114 Z0.200 F1800 A6.00708; Infill 
M73 P19; Update Progress 
G1 X-8.598 Y9.114 Z0.200 F1800 A6.02093; Connection 
G1 X-8.598 Y-9.114 Z0.200 F1800 A6.65818; Infill 
M73 P21; Update Progress 
G1 X-8.202 Y-9.114 Z0.200 F1800 A6.67202; Connection 
G1 X-8.202 Y9.114 Z0.200 F1800 A7.30927; Infill 
M73 P22; Update Progress 
G1 X-7.806 Y9.114 Z0.200 F1800 A7.32311; Connection 
M73 P23; Update Progress 
G1 X-7.806 Y-9.114 Z0.200 F1800 A7.96036; Infill 
M73 P24; Update Progress 
G1 X-7.410 Y-9.114 Z0.200 F1800 A7.97420; Connection 
G1 X-7.410 Y9.114 Z0.200 F1800 A8.61145; Infill 
M73 P26; Update Progress 
G1 X-7.014 Y9.114 Z0.200 F1800 A8.62530; Connection 
G1 X-7.014 Y-9.114 Z0.200 F1800 A9.26254; Infill 
M73 P28; Update Progress 
G1 X-6.618 Y-9.114 Z0.200 F1800 A9.27639; Connection 
G1 X-6.618 Y9.114 Z0.200 F1800 A9.91363; Infill 
M73 P30; Update Progress 
G1 X-6.222 Y9.114 Z0.200 F1800 A9.92748; Connection 
G1 X-6.222 Y-9.114 Z0.200 F1800 A10.56473; Infill 
M73 P31; Update Progress 
G1 X-5.826 Y-9.114 Z0.200 F1800 A10.57857; Connection 
G1 X-5.826 Y9.114 Z0.200 F1800 A11.21582; Infill 
M73 P33; Update Progress 
G1 X-5.430 Y9.114 Z0.200 F1800 A11.22966; Connection 
G1 X-5.430 Y-9.114 Z0.200 F1800 A11.86691; Infill 
M73 P35; Update Progress 
G1 X-5.034 Y-9.114 Z0.200 F1800 A11.88075; Connection 
G1 X-5.034 Y9.114 Z0.200 F1800 A12.51800; Infill 
M73 P37; Update Progress 
G1 X-4.638 Y9.114 Z0.200 F1800 A12.53184; Connection 
G1 X-4.638 Y-9.114 Z0.200 F1800 A13.16909; Infill 
M73 P39; Update Progress 
G1 X-4.242 Y-9.114 Z0.200 F1800 A13.18294; Connection 
G1 X-4.242 Y9.114 Z0.200 F1800 A13.82018; Infill 
M73 P40; Update Progress 
G1 X-3.846 Y9.114 Z0.200 F1800 A13.83403; Connection 
G1 X-3.846 Y-9.114 Z0.200 F1800 A14.47128; Infill 
M73 P42; Update Progress 
G1 X-3.450 Y-9.114 Z0.200 F1800 A14.48512; Connection 
G1 X-3.450 Y9.114 Z0.200 F1800 A15.12237; Infill 
M73 P44; Update Progress 
G1 X-3.054 Y9.114 Z0.200 F1800 A15.13621; Connection 
G1 X-3.054 Y-9.114 Z0.200 F1800 A15.77346; Infill 
M73 P46; Update Progress 
G1 X-2.658 Y-9.114 Z0.200 F1800 A15.78730; Connection 
G1 X-2.658 Y9.114 Z0.200 F1800 A16.42455; Infill 
M73 P48; Update Progress 
G1 X-2.262 Y9.114 Z0.200 F1800 A16.43839; Connection 
G1 X-2.262 Y-9.114 Z0.200 F1800 A17.07564; Infill 
M73 P49; Update Progress 
G1 X-1.866 Y-9.114 Z0.200 F1800 A17.08949; Connection 
G1 X-1.866 Y9.114 Z0.200 F1800 A17.72673; Infill 
M73 P51; Update Progress 
G1 X-1.470 Y9.114 Z0.200 F1800 A17.74058; Connection 
G1 X-1.470 Y-9.114 Z0.200 F1800 A18.37783; Infill 
M73 P53; Update Progress 
G1 X-1.074 Y-9.114 Z0.200 F1800 A18.39167; Connection 
G1 X-1.074 Y9.114 Z0.200 F1800 A19.02892; Infill 
M73 P55; Update Progress 
G1 X-0.678 Y9.114 Z0.200 F1800 A19.04276; Connection 
G1 X-0.678 Y-9.114 Z0.200 F1800 A19.68001; Infill 
M73 P57; Update Progress 
G1 X-0.282 Y-9.114 Z0.200 F1800 A19.69385; Connection 
G1 X-0.282 Y9.114 Z0.200 F1800 A20.33110; Infill 
M73 P58; Update Progress 
G1 X0.114 Y9.114 Z0.200 F1800 A20.34494; Connection 
G1 X0.114 Y-9.114 Z0.200 F1800 A20.98219; Infill 
M73 P60; Update Progress 
G1 X0.510 Y-9.114 Z0.200 F1800 A20.99604; Connection 
G1 X0.510 Y9.114 Z0.200 F1800 A21.63328; Infill 
M73 P62; Update Progress 
G1 X0.906 Y9.114 Z0.200 F1800 A21.64713; Connection 
G1 X0.906 Y-9.114 Z0.200 F1800 A22.28437; Infill 
M73 P64; Update Progress 
G1 X1.302 Y-9.114 Z0.200 F1800 A22.29822; Connection 
G1 X1.302 Y9.114 Z0.200 F1800 A22.93547; Infill 
M73 P65; Update Progress 
G1 X1.698 Y9.114 Z0.200 F1800 A22.94931; Connection 
M73 P66; Update Progress 
G1 X1.698 Y-9.114 Z0.200 F1800 A23.58656; Infill 
M73 P67; Update Progress 
G1 X2.094 Y-9.114 Z0.200 F1800 A23.60040; Connection 
G1 X2.094 Y9.114 Z0.200 F1800 A24.23765; Infill 
M73 P69; Update Progress 
G1 X2.490 Y9.114 Z0.200 F1800 A24.25149; Connection 
G1 X2.490 Y-9.114 Z0.200 F1800 A24.88874; Infill 
M73 P71; Update Progress 
G1 X2.886 Y-9.114 Z0.200 F1800 A24.90259; Connection 
G1 X2.886 Y9.114 Z0.200 F1800 A25.53983; Infill 
M73 P73; Update Progress 
G1 X3.282 Y9.114 Z0.200 F1800 A25.55368; Connection 
G1 X3.282 Y-9.114 Z0.200 F1800 A26.19092; Infill 
M73 P74; Update Progress 
G1 X3.678 Y-9.114 Z0.200 F1800 A26.20477; Connection 
G1 X3.678 Y9.114 Z0.200 F1800 A26.84202; Infill 
M73 P76; Update Progress 
G1 X4.074 Y9.114 Z0.200 F1800 A26.85586; Connection 
G1 X4.074 Y-9.114 Z0.200 F1800 A27.49311; Infill 
M73 P78; Update Progress 
G1 X4.470 Y-9.114 Z0.200 F1800 A27.50695; Connection 
G1 X4.470 Y9.114 Z0.200 F1800 A28.14420; Infill 
M73 P80; Update Progress 
G1 X4.866 Y9.114 Z0.200 F1800 A28.15804; Connection 
G1 X4.866 Y-9.114 Z0.200 F1800 A28.79529; Infill 
M73 P82; Update Progress 
G1 X5.262 Y-9.114 Z0.200 F1800 A28.80914; Connection 
G1 X5.262 Y9.114 Z0.200 F1800 A29.44638; Infill 
M73 P83; Update Progress 
G1 X5.658 Y9.114 Z0.200 F1800 A29.46023; Connection 
G1 X5.658 Y-9.114 Z0.200 F1800 A30.09747; Infill 
M73 P85; Update Progress 
G1 X6.054 Y-9.114 Z0.200 F1800 A30.11132; Connection 
G1 X6.054 Y9.114 Z0.200 F1800 A30.74857; Infill 
M73 P87; Update Progress 
G1 X6.450 Y9.114 Z0.200 F1800 A30.76241; Connection 
G1 X6.450 Y-9.114 Z0.200 F1800 A31.39966; Infill 
M73 P89; Update Progress 
G1 X6.846 Y-9.114 Z0.200 F1800 A31.41350; Connection 
G1 X6.846 Y9.114 Z0.200 F1800 A32.05075; Infill 
M73 P91; Update Progress 
G1 X7.242 Y9.114 Z0.200 F1800 A32.06459; Connection 
G1 X7.242 Y-9.114 Z0.200 F1800 A32.70184; Infill 
M73 P92; Update Progress 
G1 X7.638 Y-9.114 Z0.200 F1800 A32.71568; Connection 
G1 X7.638 Y9.114 Z0.200 F1800 A33.35293; Infill 
M73 P94; Update Progress 
G1 X8.034 Y9.114 Z0.200 F1800 A33.36678; Connection 
G1 X8.034 Y-9.114 Z0.200 F1800 A34.00402; Infill 
M73 P96; Update Progress 
G1 X8.430 Y-9.114 Z0.200 F1800 A34.01787; Connection 
G1 X8.430 Y9.114 Z0.200 F1800 A34.65512; Infill 
M73 P98; Update Progress 
G1 X8.826 Y9.114 Z0.200 F1800 A34.66896; Connection 
G1 X8.826 Y-9.114 Z0.200 F1800 A35.30621; Infill 
M73 P100; Update Progress 
; End of print 
G1 X8.826 Y-9.114 Z0.200 F1500 A34.00621; Retract 
M127 T0 (Fan Off)
M18 A B(Turn off A and B Steppers)
G1 Z155 F900
G162 X Y F2000
M18 X Y Z(Turn off steppers after a build)
M104 S0 T0
M70 P5 (We <3 Making Things!)
M72 P1  ( Play Ta-Da song )
M73 P100
M137 (build end notification)
