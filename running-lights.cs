Vector column;
Vector3D[][] lightTable;

double min_dist = 0;
var door = ag.doors[0];
var first = lights[0].GetPosition();
for (int i = 10; i < lights.length; i++) {
	var d = Vector3D.Distance(first, lights[i].GetPosition());
	if (d < min_dist || column == null) {
		min_dist = d;
		column = i;
	}
}
for (int i=1; i < lights.length; i++) {
  	if (Vector3D.Distance(light[i].GetPosition(), lights[0].GetPosition())
}
	
